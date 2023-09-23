using CsvHelper.Configuration.Attributes;

namespace DbIpToSql
{
    internal sealed class IpLocationInfo
    {
        [Index(0)]
        public string? StartIpAddress { get; set; }
        [Index(1)]
        public string? EndIpAddress { get; set; }
        [Index(2)]
        public string? Continent { get; set; }
        [Index(3)]
        public string? Country { get; set; }
        [Index(4)]
        public string? Region { get; set; }
        [Index(5)]
        public string? City { get; set; }
        [Index(6)]
        public decimal Latitude { get; set; }
        [Index(7)]
        public decimal Longitude { get; set; }
        [Ignore]
        public int AddressFamily { get; set; }
        [Ignore]
        public byte[]? StartIpAddressBytes { get; set; }
        [Ignore]
        public byte[]? EndIpAddressBytes { get; set; }
    }
}
