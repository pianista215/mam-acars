using FSUIPC;
using MamAcars.Models;
using MamAcars.Utils;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
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

        private BlackBoxBasicInformation _previousState;

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
                    Console.WriteLine($"Error cleaning FlightsPath {ex.Message}");
                }
            }
        }

        public class StoredFlightData
        {
            public long FlightId { get; set; }
            public string Aircraft { get; set; }
            public string? PilotComment { get; set; }
            public string? ReportId { get; set; }
        }

        public void RegisterFlight(long flightId, string aircraft)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"INSERT INTO flights (id, aircraft) VALUES (@id, @aircraft);";
            command.Parameters.AddWithValue("@id", flightId);
            command.Parameters.AddWithValue("@aircraft", aircraft);
            command.ExecuteNonQuery();

            _previousState = null; // Reset the state for a new flight
        }

        public StoredFlightData GetPendingFlight()
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT id, aircraft, pilot_comment, report_id FROM flights WHERE pilot_comment IS NOT NULL LIMIT 1";
            using var reader = command.ExecuteReader();

            if (reader.Read())
            {
                return new StoredFlightData
                {
                    FlightId = reader.GetInt64(0),
                    Aircraft = reader.GetString(1),
                    PilotComment = reader.GetString(2),
                    ReportId = reader.IsDBNull(3) ? null : reader.GetString(3)
                };
            } else
            {
                return null;
            }
        }

        public void SetComment(long flightId, string comment)
        {
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
            return result;
        }

        public void SetReportId(long flightId, string reportId)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"UPDATE flights SET report_id=@report_id WHERE id=@id;";
            command.Parameters.AddWithValue("@id", flightId);
            command.Parameters.AddWithValue("@report_id", reportId);
            command.ExecuteNonQuery();
        }

        public void AddChunk(long flightId, int chunk_id, string path)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"INSERT INTO chunks(flight_id, id, path) VALUES (@flight_id, @id, @path);";
            command.Parameters.AddWithValue("@id", flightId);
            command.Parameters.AddWithValue("@id", chunk_id);
            command.Parameters.AddWithValue("@path", path);
            command.ExecuteNonQuery();
        }

        public void DeleteChunk(long flightId, int chunk_id)
        {
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

            return (formattedFirst, formattedLast);
        } 

        public void RecordEvent(long? flightId, BlackBoxBasicInformation currentState)
        {
            var changes = GetChanges(_previousState, currentState);

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

                _previousState = currentState;
            }
        }

        private List<KeyValuePair<string, object>> GetChanges(BlackBoxBasicInformation previous, BlackBoxBasicInformation current)
        {
            var changes = new List<KeyValuePair<string, object>>();

            // TODO: UNAI RETHINK THAT BECAUSE WE NEED TO TRY TO GET A LOT OF DATA NEAR TERRAIN, BUT LESS HIGHER

            if (previous == null || previous.onGround != current.onGround)
            {
                // If "onGround" changes, record all values
                changes.Add(new KeyValuePair<string, object>("Latitude", current.Latitude));
                changes.Add(new KeyValuePair<string, object>("Longitude", current.Longitude));
                changes.Add(new KeyValuePair<string, object>("onGround", current.onGround));
                changes.Add(new KeyValuePair<string, object>("Altitude", current.Altitude));
                changes.Add(new KeyValuePair<string, object>("AGLAltitude", current.AGLAltitude));
                changes.Add(new KeyValuePair<string, object>("Heading", current.Heading));
                changes.Add(new KeyValuePair<string, object>("GroundSpeedKnots", current.GroundSpeedKnots));
                return changes;
            }

            if (previous == null || Math.Abs(previous.Altitude - current.Altitude) > 800)
            {
                if (previous == null || previous.Latitude != current.Latitude)
                    changes.Add(new KeyValuePair<string, object>("Latitude", current.Latitude));

                if (previous == null || previous.Longitude != current.Longitude)
                    changes.Add(new KeyValuePair<string, object>("Longitude", current.Longitude));

                changes.Add(new KeyValuePair<string, object>("Altitude", current.Altitude));
            }

            if (previous == null || Math.Abs(previous.Heading - current.Heading) > 30)
            {
                if (previous == null || previous.Latitude != current.Latitude)
                    changes.Add(new KeyValuePair<string, object>("Latitude", current.Latitude));

                if (previous == null || previous.Longitude != current.Longitude)
                    changes.Add(new KeyValuePair<string, object>("Longitude", current.Longitude));

                changes.Add(new KeyValuePair<string, object>("Heading", current.Heading));
            }

            if (previous == null || Math.Abs(previous.GroundSpeedKnots - current.GroundSpeedKnots) > 20)
            {
                if (previous == null || previous.Latitude != current.Latitude)
                    changes.Add(new KeyValuePair<string, object>("Latitude", current.Latitude));

                if (previous == null || previous.Longitude != current.Longitude)
                    changes.Add(new KeyValuePair<string, object>("Longitude", current.Longitude));

                changes.Add(new KeyValuePair<string, object>("GroundSpeedKnots", current.GroundSpeedKnots));
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
            var json = GenerateJson(flightId);
            var jsonFilePath = GetFlightJsonFilePath(flightId);
            var gzipFilePath = GetFlightGzipFilePath(flightId);
            FileHandler.WriteToFile(jsonFilePath, json);
            FileHandler.CompressFile(jsonFilePath, gzipFilePath);
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
