﻿namespace DbIpToSql
{
    internal sealed class AppSettings
    {
        public required int RecordsPerUpload { get; init; }
        public required ConnectionStrings ConnectionStrings { get; init; }

        public void Validate()
        {
            // RecordsPerUpload
            if (RecordsPerUpload < 1000)
            {
                throw new AppSettingsException("AppSettings.RecordsPerUpload must be an integer greater than or equal to 1000.");
            }

            // ConnectionStrings
            if (string.IsNullOrEmpty(ConnectionStrings?.DbIpLocation))
            {
                throw new AppSettingsException("AppSettings.ConnectionStrings.DbIpLocation must be a string which is not null or empty.");
            }
        }
    }

    internal sealed class ConnectionStrings
    {
        public required string DbIpLocation { get; init; }
    }
}
