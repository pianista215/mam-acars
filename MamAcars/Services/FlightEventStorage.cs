using FSUIPC;
using MamAcars.Models;
using MamAcars.Utils;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Timers;
using System.Xml.Linq;

namespace MamAcars.Services
{
    public class FlightEventStorage
    {
        private readonly SQLiteConnection _connection;
        private readonly List<(int eventId, string variable, object value)> _eventBuffer = new();
        private readonly Timer _batchWriteTimer;
        private readonly object _lock = new();

        // The entries of that BlackboxInformation could be from different timestamps
        private BlackBoxBasicInformation _lastLoggedVars = new BlackBoxBasicInformation();
        private DateTime? _lastFullWritten = null;

        private static readonly string FilePath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MamAcars", "sqlite.dat");

        private static readonly string FlightsPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MamAcars", "flights");

        private string GetFlightJsonFilePath(long flightId)
        {
            Directory.CreateDirectory(FlightsPath!);
            return Path.Combine(FlightsPath, $"{flightId}.json");
        }

        private string GetFlightGzipFilePath(long flightId)
        {
            Directory.CreateDirectory(FlightsPath!);
            return Path.Combine(FlightsPath, $"{flightId}.gz");
        }


        public FlightEventStorage()
        {
            var connectionString = $"Data Source={FilePath};Version=3;";
            _connection = new SQLiteConnection(connectionString);
            _connection.Open();

            if (!File.Exists(FilePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                SQLiteConnection.CreateFile(FilePath);
            }

            EnsureSchema();

            _batchWriteTimer = new Timer(10000); // 10 seconds
            _batchWriteTimer.Elapsed += (sender, e) => WriteBatchToDatabase();
            _batchWriteTimer.Start();
        }

        private void EnsureSchema()
        {
            string createFlightsTable = @"CREATE TABLE IF NOT EXISTS flights (
            id INTEGER PRIMARY KEY,
            aircraft TEXT NOT NULL,
            network TEXT NOT NULL,
            pilot_comment TEXT DEFAULT NULL,
            report_id TEXT DEFAULT NULL
        );";

            string createEventTable = @"CREATE TABLE IF NOT EXISTS events (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            flight_id INTEGER NOT NULL,
            timestamp DATETIME NOT NULL,
            FOREIGN KEY (flight_id) REFERENCES flights(id)
        );";

            string createChangesTable = @"CREATE TABLE IF NOT EXISTS changes (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            event_id INTEGER NOT NULL,
            variable TEXT NOT NULL,
            value TEXT NOT NULL,
            FOREIGN KEY (event_id) REFERENCES events(id)
        );";

            string pendingChunksTable = @"CREATE TABLE IF NOT EXISTS chunks (
            flight_id INTEGER NOT NULL,
            id INTEGER NOT NULL,
            path TEXT NOT NULL,
            FOREIGN KEY (flight_id) REFERENCES flights(id)
        );";

            using var command = _connection.CreateCommand();
            command.CommandText = createFlightsTable;
            command.ExecuteNonQuery();

            command.CommandText = createEventTable;
            command.ExecuteNonQuery();

            command.CommandText = createChangesTable;
            command.ExecuteNonQuery();

            command.CommandText = pendingChunksTable;
            command.ExecuteNonQuery();
        }

        public void CleanAllData()
        {
            using var transaction = _connection.BeginTransaction();

            using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM changes;";
            command.ExecuteNonQuery();

            command.CommandText = "DELETE FROM sqlite_sequence WHERE name = 'changes'";
            command.ExecuteNonQuery();

            command.CommandText = "DELETE FROM events;";
            command.ExecuteNonQuery();

            command.CommandText = "DELETE FROM sqlite_sequence WHERE name = 'events'";
            command.ExecuteNonQuery();

            command.CommandText = "DELETE FROM chunks;";
            command.ExecuteNonQuery();

            command.CommandText = "DELETE FROM flights;";
            command.ExecuteNonQuery();

            transaction.Commit();

            ClearFlightsFolder();
        }

        private void ClearFlightsFolder()
        {
            if (Directory.Exists(FlightsPath))
            {
                try
                {
                    Directory.Delete(FlightsPath, true);
                }
                catch (Exception ex)
                {
                    Log.Error("Error cleaning FlightPath", ex);
                }
            }
        }

        public class StoredFlightData
        {
            public long FlightId { get; set; }
            public string Aircraft { get; set; }
            public string Network { get; set; }
            public string? PilotComment { get; set; }
            public string? ReportId { get; set; }
        }

        public void RegisterFlight(long flightId, string aircraft, string network)
        {
            Log.Information($"Registering flight {flightId} aircraft {aircraft} network {network}");
            using var command = _connection.CreateCommand();
            command.CommandText = @"INSERT INTO flights (id, aircraft, network) VALUES (@id, @aircraft, @network);";
            command.Parameters.AddWithValue("@id", flightId);
            command.Parameters.AddWithValue("@aircraft", aircraft);
            command.Parameters.AddWithValue("@network", network);
            command.ExecuteNonQuery();
        }

        public StoredFlightData GetPendingFlight()
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT id, aircraft, network, pilot_comment, report_id FROM flights WHERE pilot_comment IS NOT NULL LIMIT 1";
            using var reader = command.ExecuteReader();

            if (reader.Read())
            {
                return new StoredFlightData
                {
                    FlightId = reader.GetInt64(0),
                    Aircraft = reader.GetString(1),
                    Network = reader.GetString(2),
                    PilotComment = reader.GetString(3),
                    ReportId = reader.IsDBNull(4) ? null : reader.GetString(3)
                };
            } else
            {
                return null;
            }
        }

        public void SetComment(long flightId, string comment)
        {
            Log.Information($"Setting comment in {flightId} : {comment}");
            using var command = _connection.CreateCommand();
            command.CommandText = @"UPDATE flights SET pilot_comment=@pilot_comment WHERE id=@id;";
            command.Parameters.AddWithValue("@id", flightId);
            command.Parameters.AddWithValue("@pilot_comment", comment);
            command.ExecuteNonQuery();
        }

        public string GetComment(long flightId)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"SELECT pilot_comment FROM flights WHERE id=@id;";
            command.Parameters.AddWithValue("@id", flightId);

            var result = command.ExecuteScalar() as string;
            Log.Information($"Retrieved comment for flight {flightId}: {result}");
            return result;
        }

        public void SetReportId(long flightId, string reportId)
        {
            Log.Information($"Saving reportId flight {flightId} : {reportId}");
            using var command = _connection.CreateCommand();
            command.CommandText = @"UPDATE flights SET report_id=@report_id WHERE id=@id;";
            command.Parameters.AddWithValue("@id", flightId);
            command.Parameters.AddWithValue("@report_id", reportId);
            command.ExecuteNonQuery();
        }

        public void AddChunk(long flightId, int chunk_id, string path)
        {
            Log.Information($"Adding chunk for flight {flightId}: {chunk_id} - {path}");
            using var command = _connection.CreateCommand();
            command.CommandText = @"INSERT INTO chunks(flight_id, id, path) VALUES (@flight_id, @id, @path);";
            command.Parameters.AddWithValue("@id", flightId);
            command.Parameters.AddWithValue("@id", chunk_id);
            command.Parameters.AddWithValue("@path", path);
            command.ExecuteNonQuery();
        }

        public void DeleteChunk(long flightId, int chunk_id)
        {
            Log.Information($"Deleting chunk for flight {flightId}: {chunk_id}");
            using var command = _connection.CreateCommand();
            command.CommandText = @"DELETE FROM chunks WHERE flight_id = @flight_id AND id = @id;";
            command.Parameters.AddWithValue("@flight_id", flightId);
            command.Parameters.AddWithValue("@id", chunk_id);
            command.ExecuteNonQuery();
        }


        public List<(int chunk_id, string path)> GetPendingChunks(long flightId)
        {
            var chunks = new List<(int, string)>();

            using var command = _connection.CreateCommand();
            command.CommandText = @"SELECT id, path FROM chunks WHERE flight_id = @flightId;";
            command.Parameters.AddWithValue("@flightId", flightId);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                int id = reader.GetInt32(0);
                string path = reader.GetString(1);
                chunks.Add((id, path));
            }

            Log.Information($"Retrieved pending chunks for flight {flightId}: {String.Join(",", chunks)}");

            return chunks;
        }

        public double GetLastLatitude(long flightId)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"
                SELECT c.value
                FROM changes c
                JOIN events e ON c.event_id = e.id
                WHERE c.variable = 'Latitude' AND e.flight_id=@id
                ORDER BY e.timestamp DESC
                LIMIT 1;";
            command.Parameters.AddWithValue("@id", flightId);

            var result = command.ExecuteScalar() as string;
            Log.Information($"Retrieved last latitude for flight {flightId}: {result}");
            return Double.Parse(result);
        }

        public double GetLastLongitude(long flightId)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"
                SELECT c.value
                FROM changes c
                JOIN events e ON c.event_id = e.id
                WHERE c.variable = 'Longitude' AND e.flight_id=@id
                ORDER BY e.timestamp DESC
                LIMIT 1;";
            command.Parameters.AddWithValue("@id", flightId);

            var result = command.ExecuteScalar() as string;
            Log.Information($"Retrieved last longitude for flight {flightId}: {result}");
            return Double.Parse(result);
        }

        public (string startTime, string endTime) GetStartAndEndTime(long flightId)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"
                SELECT MIN(timestamp), MAX(timestamp) FROM events WHERE flight_id=@id;";
            command.Parameters.AddWithValue("@id", flightId);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
                throw new InvalidOperationException("No rows for this flightId in the events.");

            DateTime firstTimestamp = DateTime.Parse(reader.GetString(0));
            DateTime lastTimestamp = DateTime.Parse(reader.GetString(1));

            string formattedFirst = firstTimestamp.ToString("yyyy-MM-dd HH:mm:ss");
            string formattedLast = lastTimestamp.ToString("yyyy-MM-dd HH:mm:ss");

            Log.Information($"Retrieved start/end time for flight {flightId}: {formattedFirst} - {formattedLast}");

            return (formattedFirst, formattedLast);
        } 

        public void RecordEvent(long? flightId, BlackBoxBasicInformation currentState)
        {
            var changes = GetChanges(currentState);

            if (changes.Any())
            {
                lock (_lock)
                {

                    using var transaction = _connection.BeginTransaction();

                    // Insert event
                    using var eventCommand = _connection.CreateCommand();
                    eventCommand.CommandText = @"INSERT INTO events (flight_id, timestamp) VALUES (@flight_id, @timestamp);";
                    eventCommand.Parameters.AddWithValue("@flight_id", flightId);
                    eventCommand.Parameters.AddWithValue("@timestamp", currentState.Timestamp);
                    eventCommand.ExecuteNonQuery();

                    long eventId = _connection.LastInsertRowId;

                    // Insert changes
                    using var changeCommand = _connection.CreateCommand();
                    changeCommand.CommandText = @"INSERT INTO changes (event_id, variable, value) VALUES (@event_id, @variable, @value);";

                    foreach (var change in changes)
                    {
                        changeCommand.Parameters.Clear();
                        changeCommand.Parameters.AddWithValue("@event_id", eventId);
                        changeCommand.Parameters.AddWithValue("@variable", change.Key);
                        changeCommand.Parameters.AddWithValue("@value", change.Value.ToString());
                        changeCommand.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
            }
        }

        private KeyValuePair<string, object> updateLatitude(double latitude)
        {
            _lastLoggedVars.Latitude = latitude;
            return new KeyValuePair<string, object>("Latitude", latitude);
        }

        private KeyValuePair<string, object> updateLongitude(double longitude)
        {
            _lastLoggedVars.Longitude = longitude;
            return new KeyValuePair<string, object>("Longitude", longitude);
        }

        private KeyValuePair<string, object> updateOnGround(bool onGround)
        {
            _lastLoggedVars.OnGround = onGround;
            return new KeyValuePair<string, object>("onGround", onGround);
        }

        private KeyValuePair<string, object> updateAltitude(int altitude)
        {
            _lastLoggedVars.Altitude = altitude;
            return new KeyValuePair<string, object>("Altitude", altitude);
        }

        private KeyValuePair<string, object> updateAGLAltitude(int aglAltitude)
        {
            _lastLoggedVars.AGLAltitude = aglAltitude;
            return new KeyValuePair<string, object>("AGLAltitude", aglAltitude);
        }

        private KeyValuePair<string, object> updateAltimeter(int altimeter)
        {
            _lastLoggedVars.Altimeter = altimeter;
            return new KeyValuePair<string, object>("Altimeter", altimeter);
        }

        private KeyValuePair<string, object> updateVerticalSpeed(int verticalSpeedFpm)
        {
            _lastLoggedVars.VerticalSpeedFPM = verticalSpeedFpm;
            return new KeyValuePair<string, object>("VSFpm", verticalSpeedFpm);
        }

        private KeyValuePair<string, object> updateLandingVsSpeed(int landingVsFpm)
        {
            _lastLoggedVars.LandingVSFPM = landingVsFpm;
            return new KeyValuePair<string, object>("LandingVSFpm", landingVsFpm);
        }

        private KeyValuePair<string, object> updateSquawk(int squawk)
        {
            _lastLoggedVars.Squawk = squawk;
            return new KeyValuePair<string, object>("Squawk", squawk);
        }

        private KeyValuePair<string, object> updateAPMaster(bool apMaster)
        {
            _lastLoggedVars.APMaster = apMaster;
            string value = apMaster ? "On" : "Off";
            return new KeyValuePair<string, object>("AP", value);
        }

        private KeyValuePair<string, object> updateHeading(int heading)
        {
            _lastLoggedVars.Heading = heading;
            return new KeyValuePair<string, object>("Heading", heading);
        }

        private KeyValuePair<string, object> updateGSKnots(int gsKnots)
        {
            _lastLoggedVars.GroundSpeedKnots = gsKnots;
            return new KeyValuePair<string, object>("GSKnots", gsKnots);
        }

        private KeyValuePair<string, object> updateIASKnots(int iasKnots)
        {
            _lastLoggedVars.IasKnots = iasKnots;
            return new KeyValuePair<string, object>("IASKnots", iasKnots);
        }

        private KeyValuePair<string, object> updateQNHSet(int qnh)
        {
            _lastLoggedVars.QnhSet = qnh;
            return new KeyValuePair<string, object>("QNHSet", qnh);
        }

        private KeyValuePair<string, object> updateFlaps(int flaps)
        {
            _lastLoggedVars.FlapsPercentage = flaps;
            return new KeyValuePair<string, object>("Flaps", flaps);
        }

        private KeyValuePair<string, object> updateGear(bool gearUp)
        {
            _lastLoggedVars.GearUp = gearUp;

            string value = gearUp ? "Up" : "Down";
            return new KeyValuePair<string, object>("Gear", value);
        }

        private KeyValuePair<string, object> updateFuelKg(double fuelKg)
        {
            _lastLoggedVars.AircraftFuelKg = fuelKg;
            return new KeyValuePair<string, object>("FuelKg", fuelKg);
        }

        private KeyValuePair<string, object> updateEngineStatus(bool engine, int enginePos)
        {
            _lastLoggedVars.EnginesStarted[enginePos] = engine;
            string engineState = engine ? "On" : "Off";
            return new KeyValuePair<string, object>($"Engine {enginePos + 1}", engineState);
        }

        private KeyValuePair<string, object>[] updateEnginesStatus(bool[] engines)
        {
            var result = new KeyValuePair<string, object>[engines.Length];

            if(_lastLoggedVars.EnginesStarted == null)
            {
                _lastLoggedVars.EnginesStarted = new bool[engines.Length];
            }

            for (int i = 0; i < engines.Length; i++)
            {
                result[i] = updateEngineStatus(engines[i], i);
            }

            return result;
        }

        private bool ShouldLogFullState(DateTime now, BlackBoxBasicInformation current)
        {
            if (_lastFullWritten == null) return true;

            if (_lastLoggedVars.OnGround != current.OnGround) return true;

            var minutesSinceLastLog = (now - _lastFullWritten.Value).TotalMinutes;
            if (minutesSinceLastLog >= 1) return true;

            var secondsSinceLastLog = (now - _lastFullWritten.Value).TotalSeconds;
            if (!current.OnGround && current.AGLAltitude <= 1000 && secondsSinceLastLog >= 10) return true;

            return false;
        }

        private bool ShouldLogLanding(BlackBoxBasicInformation current)
        {
            return _lastFullWritten != null && current.OnGround && _lastLoggedVars.OnGround == false;
        }

        private List<KeyValuePair<string, object>> GetChanges(BlackBoxBasicInformation current)
        {
            var changes = new List<KeyValuePair<string, object>>();

            var now = DateTime.UtcNow;

            if (ShouldLogLanding(current))
            {
                changes.Add(updateLandingVsSpeed(current.LandingVSFPM));
            }

            if (this.ShouldLogFullState(now, current))
            {
                changes.Add(updateLatitude(current.Latitude));
                changes.Add(updateLongitude(current.Longitude));
                changes.Add(updateOnGround(current.OnGround));
                changes.Add(updateAltitude(current.Altitude));
                changes.Add(updateAGLAltitude(current.AGLAltitude));
                changes.Add(updateAltimeter(current.Altimeter));
                changes.Add(updateVerticalSpeed(current.VerticalSpeedFPM));
                changes.Add(updateHeading(current.Heading));
                changes.Add(updateGSKnots(current.GroundSpeedKnots));
                changes.Add(updateIASKnots(current.IasKnots));
                changes.Add(updateQNHSet(current.QnhSet));
                changes.Add(updateFlaps(current.FlapsPercentage));
                changes.Add(updateGear(current.GearUp));
                changes.Add(updateFuelKg(current.AircraftFuelKg));
                changes.Add(updateSquawk(current.Squawk));
                changes.Add(updateAPMaster(current.APMaster));

                var enginesChanges = updateEnginesStatus(current.EnginesStarted);
                foreach (var engineChange in enginesChanges)
                {
                    changes.Add(engineChange);
                }

                //Log.Information($"NEW STATUS: {_lastLoggedVars}");

                this._lastFullWritten = now;
            }
            else
            {
                if (Math.Abs(_lastLoggedVars.Altitude - current.Altitude) > 800 || Math.Abs(_lastLoggedVars.VerticalSpeedFPM - current.VerticalSpeedFPM) > 400)
                {
                    changes.Add(updateLatitude(current.Latitude));
                    changes.Add(updateLongitude(current.Longitude));
                    changes.Add(updateAltitude(current.Altitude));
                    changes.Add(updateAGLAltitude(current.AGLAltitude));
                    changes.Add(updateAltimeter(current.Altimeter));
                    changes.Add(updateVerticalSpeed(current.VerticalSpeedFPM));
                }

                if (Math.Abs(_lastLoggedVars.Heading - current.Heading) > 25)
                {
                    changes.Add(updateLatitude(current.Latitude));
                    changes.Add(updateLongitude(current.Longitude));
                    changes.Add(updateHeading(current.Heading));
                }

                if (Math.Abs(_lastLoggedVars.IasKnots - current.IasKnots) > 15)
                {
                    changes.Add(updateLatitude(current.Latitude));
                    changes.Add(updateLongitude(current.Longitude));
                    changes.Add(updateGSKnots(current.GroundSpeedKnots));
                    changes.Add(updateIASKnots(current.IasKnots));
                }

                if (_lastLoggedVars.QnhSet != current.QnhSet)
                {
                    changes.Add(updateLatitude(current.Latitude));
                    changes.Add(updateLongitude(current.Longitude));
                    changes.Add(updateAltitude(current.Altitude));
                    changes.Add(updateAGLAltitude(current.AGLAltitude));
                    changes.Add(updateAltimeter(current.Altimeter));
                    changes.Add(updateQNHSet(current.QnhSet));
                }

                if (_lastLoggedVars.FlapsPercentage != current.FlapsPercentage)
                {
                    changes.Add(updateLatitude(current.Latitude));
                    changes.Add(updateLongitude(current.Longitude));
                    changes.Add(updateFlaps(current.FlapsPercentage));
                }

                if (_lastLoggedVars.GearUp != current.GearUp)
                {
                    changes.Add(updateLatitude(current.Latitude));
                    changes.Add(updateLongitude(current.Longitude));
                    changes.Add(updateGear(current.GearUp));
                }

                if(_lastLoggedVars.Squawk != current.Squawk)
                {
                    changes.Add(updateSquawk(current.Squawk));
                }

                if (_lastLoggedVars.APMaster != current.APMaster)
                {
                    changes.Add(updateAPMaster(current.APMaster));
                }

                for (int i = 0; i < current.EnginesStarted.Length; i++)
                {
                    if (_lastLoggedVars.EnginesStarted[i] != current.EnginesStarted[i])
                    {
                        changes.Add(updateEngineStatus(current.EnginesStarted[i], i));
                    }
                }

            }
            return changes;
        }


        private void WriteBatchToDatabase()
        {
            lock (_lock)
            {
                if (!_eventBuffer.Any()) return;

                using var transaction = _connection.BeginTransaction();
                using var changeCommand = _connection.CreateCommand();
                changeCommand.CommandText = @"INSERT INTO changes (event_id, variable, value) VALUES (@event_id, @variable, @value);";

                foreach (var (eventId, variable, value) in _eventBuffer)
                {
                    changeCommand.Parameters.Clear();
                    changeCommand.Parameters.AddWithValue("@event_id", eventId);
                    changeCommand.Parameters.AddWithValue("@variable", variable);
                    changeCommand.Parameters.AddWithValue("@value", value.ToString());
                    changeCommand.ExecuteNonQuery();
                }

                transaction.Commit();
                _eventBuffer.Clear();
            }
        }

        public async Task<string> ExportAndCompressFlightToJson(long flightId)
        {
            // TODO: Think, maybe memory could be exhausted and it's better to generate and write by steps
            var json = GenerateJson(flightId);
            var jsonFilePath = GetFlightJsonFilePath(flightId);
            var gzipFilePath = GetFlightGzipFilePath(flightId);
            Log.Information($"Writting json file {flightId}: {jsonFilePath}");
            FileHandler.WriteToFile(jsonFilePath, json);
            Log.Information($"Compressing json file {flightId}: {jsonFilePath} -> {gzipFilePath}");
            FileHandler.CompressFile(jsonFilePath, gzipFilePath);
            Log.Information("Compression OK");
            return gzipFilePath;
        }

        private class FlightJsonData
        {
            public long FlightId { get; set; }
            public List<FlightJsonEvent> Events { get; set; }
        }

        private class FlightJsonEvent
        {
            public DateTime Timestamp { get; set; }
            public Dictionary<string, object> Changes { get; set; }
        }

        private string GenerateJson(long flightId)
        {
            Log.Information($"Generating json for flight {flightId}");
            var flightData = new FlightJsonData
            {
                FlightId = flightId,
                Events = new List<FlightJsonEvent>()
            };

            // Query events
            using var eventCommand = _connection.CreateCommand();
            eventCommand.CommandText = "SELECT id, timestamp FROM events WHERE flight_id = @flight_id";
            eventCommand.Parameters.AddWithValue("@flight_id", flightId);

            using var eventReader = eventCommand.ExecuteReader();
            while (eventReader.Read())
            {
                var eventId = eventReader.GetInt64(0);
                var timestamp = eventReader.GetDateTime(1);

                var changes = new Dictionary<string, object>();

                // Query changes for this event
                using var changeCommand = _connection.CreateCommand();
                changeCommand.CommandText = "SELECT variable, value FROM changes WHERE event_id = @event_id";
                changeCommand.Parameters.AddWithValue("@event_id", eventId);

                using var changeReader = changeCommand.ExecuteReader();
                while (changeReader.Read())
                {
                    var variable = changeReader.GetString(0);
                    var value = changeReader.GetValue(1); // Handles different data types
                    changes[variable] = value;
                }

                flightData.Events.Add(new FlightJsonEvent
                {
                    Timestamp = timestamp,
                    Changes = changes
                });
            }

            // Serialize to JSON
            var options = new JsonSerializerOptions { WriteIndented = true };
            return JsonSerializer.Serialize(flightData, options);
        }



        public void Dispose()
        {
            _batchWriteTimer.Stop();
            _connection?.Close();
        }
    }
}
