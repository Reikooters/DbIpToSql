using CsvHelper;
using CsvHelper.Configuration;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace DbIpToSql
{
    internal sealed class Application
    {
        private readonly AppSettings _appSettings;
        private readonly IHttpClientFactory _httpClientFactory;

        private static Regex csvDownloadUrlRegex = new Regex("https://download\\.db-ip\\.com/free/dbip-city-lite-20[0-9]{2}-(0?[1-9]|[1][0-2])\\.csv\\.gz", RegexOptions.NonBacktracking | RegexOptions.Compiled);

        public Application(AppSettings appSettings,
            IHttpClientFactory httpClientFactory)
        {
            _appSettings = appSettings;
            _httpClientFactory = httpClientFactory;
        }

#if DEBUG
        public static bool debug { get; set; } = true;
#else
        public static bool debug { get; set; } = false;
#endif

        public async Task<int> StartAsync(string[] args)
        {
            try
            {
                SqlMapper.Settings.CommandTimeout = 600;

                Log.Information("Application starting.");

                Log.Information("Checking for latest file url.");
                DownloadUrlResponse downloadUrlResponse = await GetLatestDownloadUrlAsync();

                Log.Information($"Data version on website: {downloadUrlResponse.DataVersion:MMMM yyyy}");

                if (!await ShouldUpdateAsync(downloadUrlResponse.DataVersion))
                {
                    Log.Information("Database already contains the latest version. No update required.");
                    return 0;
                }

                Log.Information("Data version on website is newer than local database.");

                Log.Information($"Downloading file: {downloadUrlResponse.DownloadUrl}");
                string csvFilePath = await DownloadFileAsync(downloadUrlResponse.DownloadUrl, downloadUrlResponse.Filename);

                CsvConfiguration config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = false,
                };

                Log.Information("Creating temporary table to insert new data into.");
                string temporaryTableName = CreateTemporaryTable(downloadUrlResponse.DataVersion);
                Log.Information($"Temporary table was created: {temporaryTableName}");

                Log.Information("Parsing downloaded file.");

                List<IpLocationInfo> ipLocationInfosForUpload = new List<IpLocationInfo>();
                int totalRecords = 0;

                using (StreamReader reader = new StreamReader(csvFilePath))
                using (CsvReader csv = new CsvReader(reader, config))
                {
                    IEnumerable<IpLocationInfo> records = csv.GetRecords<IpLocationInfo>();

                    foreach (IpLocationInfo record in records)
                    {
                        if (record.StartIpAddress is not null)
                        {
                            (int ipAddressFamily, byte[]? ipBytes) = Toolbox.IpAddressToBytes(record.StartIpAddress);

                            if (ipAddressFamily == 0 || ipBytes is null)
                            {
                                throw new Exception($"Failed to parse IP address: {record.StartIpAddress}");
                            }

                            record.AddressFamily = ipAddressFamily;
                            record.StartIpAddressBytes = ipBytes;
                        }

                        if (record.EndIpAddress is not null)
                        {
                            (int ipAddressFamily, byte[]? ipBytes) = Toolbox.IpAddressToBytes(record.EndIpAddress);

                            if (ipAddressFamily == 0 || ipBytes is null)
                            {
                                throw new Exception($"Failed to parse IP address: {record.EndIpAddress}");
                            }

                            record.EndIpAddressBytes = ipBytes;
                        }

                        ipLocationInfosForUpload.Add(record);
                        ++totalRecords;

                        if (ipLocationInfosForUpload.Count >= _appSettings.RecordsPerUpload)
                        {
                            Log.Information($"Uploading batch of {ipLocationInfosForUpload.Count} records to database (total so far: {totalRecords}).");
                            UploadDataToDatabase(ipLocationInfosForUpload, temporaryTableName);
                            ipLocationInfosForUpload = new List<IpLocationInfo>();
                        }
                    }
                }

                if (ipLocationInfosForUpload.Count > 0)
                {
                    Log.Information($"Uploading last {ipLocationInfosForUpload.Count} records to database (total records: {totalRecords}).");
                    UploadDataToDatabase(ipLocationInfosForUpload, temporaryTableName);
                }

                Log.Information($@"Completing update process:
- Drop live table 'tblIpAddresses'
- Rename temp table '{temporaryTableName}' to live table 'tblIpAddresses'
- Set updated data version in database in 'tblDataVersion' table: {downloadUrlResponse.DataVersion:MMMM yyyy}.");
                FinalizeDatabaseUpload(temporaryTableName, downloadUrlResponse.DataVersion);

                Log.Information($"Deleting downloaded csv file from disk: {csvFilePath}");
                File.Delete(csvFilePath);

                Log.Information("Application finished successfully.");

                return 0;
            }
            catch (Exception e) when (!debug)
            {
                Log.Error(Toolbox.GetExceptionString(e));
                Log.Error("Application exited abnormally.");
                return -1;
            }
            finally
            {
                Log.Information($"Application total run time: {Stopwatch.GetElapsedTime(Program.AppStartTime)}");
            }
        }

        private async Task<DownloadUrlResponse> GetLatestDownloadUrlAsync()
        {
            // <a href="https://download.db-ip.com/free/dbip-city-lite-2023-09.csv.gz" class="btn btn-block icon-txt download free_download_link">Download IP to City Lite CSV</a>

            string pageUrl = "https://db-ip.com/db/download/ip-to-city-lite";

            Log.Information($"Looking up URL for latest CSV file from: {pageUrl}");

            HttpClient client = _httpClientFactory.CreateClient("db-ip");

            using (HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, pageUrl))
            {
                using (HttpResponseMessage response = await client.SendAsync(httpRequestMessage))
                {
                    response.EnsureSuccessStatusCode();

                    string pageHtml = await response.Content.ReadAsStringAsync();

                    DateTime now = DateTime.Now;
                    string filename = $"dbip-city-lite-{now:yyyy-MM}.csv.gz";
                    string urlPrefix = "https://download.db-ip.com/free/";
                    string downloadUrl = urlPrefix + filename;

                    if (pageHtml.Contains($"href=\"{downloadUrl}\""))
                    {
                        return new DownloadUrlResponse
                        {
                            DownloadUrl = downloadUrl,
                            Filename = filename,
                            DataVersion = new DateTime(now.Year, now.Month, 1),
                        };
                    }

                    Match regexMatch = csvDownloadUrlRegex.Match(pageHtml);

                    if (!regexMatch.Success)
                    {
                        throw new Exception("Could not find CSV download link.");
                    }

                    downloadUrl = regexMatch.Value;
                    filename = downloadUrl.Substring(urlPrefix.Length);

                    // Parse date (yyyy-MM) from filename
                    DateTime dataVersion = DateTime.Parse(filename.Substring("dbip-city-lite-".Length, 7) + "-01");

                    return new DownloadUrlResponse
                    {
                        DownloadUrl = downloadUrl,
                        Filename = filename,
                        DataVersion = dataVersion,
                    };
                }
            }
        }

        private async Task<bool> ShouldUpdateAsync(DateTime dataVersion)
        {
            using (SqlConnection sqlConnection = new SqlConnection(_appSettings.ConnectionStrings.DbIpLocation))
            {
                string sql = @"
select top 1 DataVersion
from tblDataVersion";

                DateTime? dbDataVersion = await sqlConnection.QueryFirstOrDefaultAsync<DateTime?>(sql);

                if (dbDataVersion is null)
                {
                    return true;
                }

                return dbDataVersion != dataVersion;
            }
        }

        private async Task<string> DownloadFileAsync(string downloadUrl, string outputFilename)
        {
            HttpClient client = _httpClientFactory.CreateClient("db-ip");

            using (HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, downloadUrl))
            {
                using (HttpResponseMessage response = await client.SendAsync(httpRequestMessage))
                {
                    response.EnsureSuccessStatusCode();

                    using (Stream responseStream = await response.Content.ReadAsStreamAsync())
                    {
                        string outputDirectory = $"{Toolbox.GetExecutableDirectory()}/download";
                        string outputPath = $"{outputDirectory}/{outputFilename}";

                        Directory.CreateDirectory(outputDirectory);

                        Log.Information($"Saving file to: {outputPath}");

                        using (FileStream fileStream = new FileStream(outputPath, FileMode.OpenOrCreate))
                        using (GZipStream gzipStream = new GZipStream(responseStream, CompressionMode.Decompress))
                        {
                            await gzipStream.CopyToAsync(fileStream);

                            return outputPath;
                        }
                    }
                }
            }
        }

        private string CreateTemporaryTable(DateTime dataVersion)
        {
            string temporaryTableName = $"tblIpAddresses_{dataVersion:yyyyMM}";

            using (SqlConnection sqlConnection = new SqlConnection(_appSettings.ConnectionStrings.DbIpLocation))
            {
                string sql = $@"
if OBJECT_ID(N'dbo.{temporaryTableName}', N'U') is not null
begin
   drop table {temporaryTableName}
end

SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON

CREATE TABLE [dbo].[{temporaryTableName}](
	[AddressFamily] [tinyint] NOT NULL,
	[StartIpAddressBytes] [varbinary](16) NOT NULL,
	[EndIpAddressBytes] [varbinary](16) NOT NULL,
	[StartIpAddress] [varchar](39) NOT NULL,
	[EndIpAddress] [varchar](39) NOT NULL,
	[Continent] [varchar](5) NULL,
	[Country] [varchar](5) NULL,
	[Region] [varchar](50) NULL,
	[City] [varchar](100) NULL,
	[Latitude] [decimal](10, 5) NULL,
	[Longitude] [decimal](10, 5) NULL
) ON [PRIMARY]
";
                sqlConnection.Execute(sql);
            }

            return temporaryTableName;
        }

        private void UploadDataToDatabase(List<IpLocationInfo> ipLocationInfo, string tempTableName)
        {
            new BulkUploadToSql<IpLocationInfo>
            {
                ConnectionString = _appSettings.ConnectionStrings.DbIpLocation,
                InternalStore = ipLocationInfo,
                TableName = tempTableName,
            }.Commit();
        }

        private void FinalizeDatabaseUpload(string temporaryTableName, DateTime dataVersion)
        {
            using (SqlConnection sqlConnection = new SqlConnection(_appSettings.ConnectionStrings.DbIpLocation))
            {
                string sql = $@"
begin transaction

-- Drop live table if it exists
if OBJECT_ID(N'dbo.tblIpAddresses', N'U') is not null
begin
   drop table tblIpAddresses
end

-- Rename temporary table to live table name
exec sp_rename 'dbo.{temporaryTableName}', 'tblIpAddresses';

-- Add index to the new table
CREATE UNIQUE CLUSTERED INDEX [IX_tblIpAddresses] ON [dbo].[tblIpAddresses]
(
	[AddressFamily] ASC,
	[StartIpAddressBytes] ASC,
	[EndIpAddressBytes] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

commit

-- Update data version
insert into tblDataVersion
(DataVersion)
select @dataVersion
where not exists
(
    select *
    from tblDataVersion
)

if @@ROWCOUNT = 0
begin
    update tblDataVersion
    set DataVersion = @dataVersion
end
";
                DynamicParameters parameters = new DynamicParameters();
                parameters.Add("@dataVersion", dataVersion, DbType.Date, ParameterDirection.Input);

                sqlConnection.Execute(sql, parameters);
            }
        }
    }
}
