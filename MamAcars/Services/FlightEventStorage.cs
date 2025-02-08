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
        private readonly string _connectionString;
        private readonly List<(int eventId, string variable, object value)> _eventBuffer = new();
        private readonly Timer _batchWriteTimer;
        private readonly object _lock = new();

        private BlackBoxBasicInformation _previousState;
        private int _currentEventId = 0;

        private static readonly string FilePath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MamAcars", "sqlite.dat");

        private static readonly string FlightsPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MamAcars", "flights");

        private string GetFlightJsonFilePath(ulong flightId)
        {
            Directory.CreateDirectory(FlightsPath!);
            return Path.Combine(FlightsPath, $"{flightId}.json");
        }

        private string GetFlightGzipFilePath(ulong flightId)
        {
            Directory.CreateDirectory(FlightsPath!);
            return Path.Combine(FlightsPath, $"{flightId}.gz");
        }


        public FlightEventStorage()
        {
            _connectionString = $"Data Source={FilePath};Version=3;";

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
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            string createFlightsTable = @"CREATE TABLE IF NOT EXISTS flights (
            id INTEGER PRIMARY KEY,
            aircraft TEXT NOT NULL,
            pilot_comment TEXT,
            start_time DATETIME NOT NULL
        );";

            string createEventTable = @"CREATE TABLE IF NOT EXISTS events (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            flight_id TEXT NOT NULL,
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

            using var command = connection.CreateCommand();
            command.CommandText = createFlightsTable;
            command.ExecuteNonQuery();

            command.CommandText = createEventTable;
            command.ExecuteNonQuery();

            command.CommandText = createChangesTable;
            command.ExecuteNonQuery();
        }

        public void RegisterFlight(ulong flightId, string aircraft)
        {
            var startTime = DateTime.UtcNow;
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO flights (id, aircraft, start_time) VALUES (@id, @aircraft, @start_time);";
            command.Parameters.AddWithValue("@id", flightId);
            command.Parameters.AddWithValue("@aircraft", aircraft);
            command.Parameters.AddWithValue("@start_time", startTime);
            command.ExecuteNonQuery();

            _previousState = null; // Reset the state for a new flight
        }

        public void SetComment(ulong flightId, string comment)
        {
            var startTime = DateTime.UtcNow;
            // TODO: REUSE CONNECTION
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"UPDATE flights SET pilot_comment=@pilot_comment WHERE id=@id;";
            command.Parameters.AddWithValue("@id", flightId);
            command.Parameters.AddWithValue("@pilot_comment", comment);
            command.ExecuteNonQuery();
        }

        public string GetComment(ulong flightId)
        {
            // TODO: REUSE CONNECTION
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"SELECT pilot_comment FROM flights WHERE id=@id;";
            command.Parameters.AddWithValue("@id", flightId);

            var result = command.ExecuteScalar() as string;
            return result;
        }

        public double GetLastLatitude(ulong flightId)
        {
            // TODO: REUSE CONNECTION
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
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

        public double GetLastLongitude(ulong flightId)
        {
            // TODO: REUSE CONNECTION
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
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

        public (string startTime, string endTime) GetStartAndEndTime(ulong flightId)
        {
            
            // TODO: REUSE CONNECTION
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
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

        public void RecordEvent(ulong? flightId, BlackBoxBasicInformation currentState)
        {
            var changes = GetChanges(_previousState, currentState);

            if (changes.Any())
            {
                lock (_lock)
                {
                    _currentEventId++;

                    using var connection = new SQLiteConnection(_connectionString);
                    connection.Open();

                    using var transaction = connection.BeginTransaction();

                    // Insert event
                    using var eventCommand = connection.CreateCommand();
                    eventCommand.CommandText = @"INSERT INTO events (flight_id, timestamp) VALUES (@flight_id, @timestamp);";
                    eventCommand.Parameters.AddWithValue("@flight_id", flightId);
                    eventCommand.Parameters.AddWithValue("@timestamp", currentState.Timestamp);
                    eventCommand.ExecuteNonQuery();

                    // Insert changes
                    using var changeCommand = connection.CreateCommand();
                    changeCommand.CommandText = @"INSERT INTO changes (event_id, variable, value) VALUES (@event_id, @variable, @value);";

                    foreach (var change in changes)
                    {
                        changeCommand.Parameters.Clear();
                        changeCommand.Parameters.AddWithValue("@event_id", _currentEventId);
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

                // TODO: REUSE CONNECTION AND DON'T INSTANTIATE ONE EACH TIME
                using var connection = new SQLiteConnection(_connectionString);
                connection.Open();

                using var transaction = connection.BeginTransaction();
                using var changeCommand = connection.CreateCommand();
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

        public async Task<string> ExportAndCompressFlightToJson(ulong flightId)
        {
            var json = GenerateJson(flightId);
            var jsonFilePath = GetFlightJsonFilePath(flightId);
            var gzipFilePath = GetFlightGzipFilePath(flightId);
            FileHandler.WriteToFile(jsonFilePath, json);
            FileHandler.CompressFile(jsonFilePath, gzipFilePath);
            return gzipFilePath;
        }

        private string GenerateJson(ulong flightId)
        {
            var flightData = new FlightData
            {
                FlightId = flightId,
                Events = new List<FlightEvent>()
            };

            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            // Query flight metadata
            using var metadataCommand = connection.CreateCommand();
            metadataCommand.CommandText = "SELECT pilot_comment, aircraft FROM flights WHERE id = @flight_id";
            metadataCommand.Parameters.AddWithValue("@flight_id", flightId);

            using var metadataReader = metadataCommand.ExecuteReader();
            if (metadataReader.Read())
            {
                flightData.PilotComments = metadataReader.GetString(0);
                flightData.Aircraft = metadataReader.GetString(1);
            }

            // Query events
            using var eventCommand = connection.CreateCommand();
            eventCommand.CommandText = "SELECT id, timestamp FROM events WHERE flight_id = @flight_id";
            eventCommand.Parameters.AddWithValue("@flight_id", flightId);

            using var eventReader = eventCommand.ExecuteReader();
            while (eventReader.Read())
            {
                var eventId = eventReader.GetInt64(0);
                var timestamp = eventReader.GetDateTime(1);

                var changes = new Dictionary<string, object>();

                // Query changes for this event
                using var changeCommand = connection.CreateCommand();
                changeCommand.CommandText = "SELECT variable, value FROM changes WHERE event_id = @event_id";
                changeCommand.Parameters.AddWithValue("@event_id", eventId);

                using var changeReader = changeCommand.ExecuteReader();
                while (changeReader.Read())
                {
                    var variable = changeReader.GetString(0);
                    var value = changeReader.GetValue(1); // Handles different data types
                    changes[variable] = value;
                }

                flightData.Events.Add(new FlightEvent
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
            WriteBatchToDatabase();
        }
    }
}
