using FSUIPC;
using MamAcars.Models;
using MamAcars.Utils;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace MamAcars.Services
{
    public class FlightContextService
    {

        private static readonly Lazy<FlightContextService> _instance = new Lazy<FlightContextService>(() => new FlightContextService());

        public static FlightContextService Instance => _instance.Value;

        private readonly ApiService _apiService;
        private readonly FlightEventStorage _storage;
        private readonly FsuipcService _fsuipcService;

        private FlightPlanInfoResponse flightPlan;

        private string gzipFlightPath;
        private List<ChunkInfo> chunks;

        private string submittedReportId;

        private FlightContextService()
        {
            _apiService = new ApiService();
            _storage = new FlightEventStorage();
            _fsuipcService = new FsuipcService(_storage);
        }

        public Boolean ExistStoredCredentials()
        {
            var token = TokenStorage.GetToken();
            return !string.IsNullOrEmpty(token);
        }

        public Boolean ExistsPendingFlightToBeSubmitted()
        {
            return _storage.GetPendingFlight() != null;
        }

        public void LoadPendingDataForSubmit()
        {
            var pendingFlight = _storage.GetPendingFlight();
            this.flightPlan = new FlightPlanInfoResponse();
            this.flightPlan.id = pendingFlight.FlightId;
            Log.Information($"Loaded pending data for submit. Flight Id: {this.flightPlan.id}");
        }

        public void CleanPreviousData()
        {
            _storage.CleanAllData();
        }

        public async Task<LoginResponse> Login(string license, string password)
        {
            var response = await _apiService.LoginAsync(license, password);
            if (response.IsSuccess)
            {
                TokenStorage.SaveToken(response.access_token);
            }
            return response;
        }

        public async Task<FlightPlanInfoResponse> LoadCurrentFlightPlan()
        {
            _apiService.SetBearerToken(TokenStorage.GetToken());

            var flightInfo = await _apiService.CurrentFlightPlanAsync();
            if(flightInfo != null)
            {
                if (flightInfo.IsSuccess)
                {
                    this.flightPlan = flightInfo;
                } else if (flightInfo.AuthFailure)
                {
                    TokenStorage.DeleteToken();
                }
            }

            return flightInfo;
        }

        public FlightPlanInfoResponse GetCurrentFlightPlan() {
            return flightPlan;
        }

        public void startMonitoringSimulator(Action<bool> SimStatusChanged, Action<bool> AircraftLocationChanged)
        {
            _fsuipcService.SimStatusChanged += SimStatusChanged;
            _fsuipcService.AircraftLocationChanged += AircraftLocationChanged;

            _fsuipcService.startLookingSimulatorAndAircraftLocation(flightPlan.departure_latitude, flightPlan.departure_longitude);
        }

        public void startSavingBlackBox()
        {
            _fsuipcService.startSavingBlackBox(flightPlan.id);
        }

        public void stopSavingBlackBox()
        {
            _fsuipcService.stopSavingBlackBox();
        }

        public void SetCommentToBlackBox(string comment)
        {
            _storage.SetComment(flightPlan.id, comment);
        }

        public async Task ExportFlightToJson()
        {
            this.gzipFlightPath = await _storage.ExportAndCompressFlightToJson(flightPlan.id);
        }

        public async Task<int> SplitBlackBoxData()
        {
            var chunksPaths = await SplitGzipFile();
            this.chunks = await ComputeSha256(chunksPaths);
            return chunks.Count;
        }

        private async Task<List<string>> SplitGzipFile()
        {
            string parentDirectory = Path.GetDirectoryName(gzipFlightPath);
            var outputPath = Path.Combine(parentDirectory, flightPlan.id.ToString());
            Directory.CreateDirectory(outputPath!);

            var chunks = await FileSplitter.SplitFileAsync(gzipFlightPath, outputPath);

            return chunks;
        }

        private class ChunkInfo
        {
            public int id { get; set; }
            public string path { get; set; }

            public string sha256sum { get; set; }
        }

        private async Task<List<ChunkInfo>> ComputeSha256(List<string> chunkPaths)
        {
            var result = new List<ChunkInfo>();
            for(int i = 0; i < chunkPaths.Count; i++)
            {
                var chunkPath = chunkPaths[i];

                var chunk = new ChunkInfo();
                chunk.id = i + 1;
                chunk.path = chunkPath;
                chunk.sha256sum = FileHandler.GenerateSha256(chunkPath);

                result.Add(chunk);
            }

            return result;
        }

        public async Task<SubmitReportResponse> SendFlightReport()
        {
            var rq = new SubmitReportRequest();

            rq.pilot_comments = _storage.GetComment(flightPlan.id);
            rq.last_position_lat = _storage.GetLastLatitude(flightPlan.id);
            rq.last_position_lon = _storage.GetLastLongitude(flightPlan.id);

            var (start, end) = _storage.GetStartAndEndTime(flightPlan.id);

            rq.start_time = start;
            rq.end_time = end;

            rq.chunks = new AcarsChunks[chunks.Count];

            for (var i = 0; i < chunks.Count; i++)
            {
                var info = chunks[i];
                var chunk = new AcarsChunks();
                chunk.id = info.id;
                chunk.sha256sum = info.sha256sum;
                rq.chunks[i] = chunk;
            }

            // TODO
            rq.network = MamUtils.GetOnlineNetwork();
            rq.sim_aircraft_name = "TODO";
            rq.report_tool = MamUtils.GetAppNameAndVersion();

            _apiService.SetBearerToken(TokenStorage.GetToken());
            var response = await _apiService.SubmitReportAsync(flightPlan.id, rq);

            if (response != null && response.IsSuccess)
            {
                this.submittedReportId = response.flight_report_id;
            }

            return response;
        }

        public async Task<UploadChunkResponse> UploadChunk(int id)
        {
            var chunkPath = chunks[id].path;
            var chunkId = chunks[id].id;

            _apiService.SetBearerToken(TokenStorage.GetToken());
            var response = await _apiService.UploadChunk(submittedReportId, chunkId, chunkPath);

            return response;
        }

    }
}
