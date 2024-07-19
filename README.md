# DbIpToSql

DbIpToSql is a tool written in C# .NET 8 which downloads the latest free-tier IP-to-Location database from the [DB-IP website](https://db-ip.com/) and stores it into a Microsoft SQL Server database.

DB-IP is a service which provides a database of IP addresses along with their city/country, which you can use to find the location of an IP address. Their database is updated monthly.

One such use case for an IP-to-Location database in your application could be to notify a user when someone logs into their account from a different country than the one they usually log in from, or in the email body of a 'Forgot Password' request to include the location where the request was made from.

This is not an official application or in any way associated with DB-IP.

## :mage: Database Attribution

Please note as per the [DB-IP licensing terms](https://db-ip.com/db/lite.php) for their free-tier database, they request providing attribution as quoted below.

> Licensing terms
> 
> The free DB-IP Lite database by [DB-IP](https://db-ip.com) is licensed under a [Creative Commons Attribution 4.0 International License](http://creativecommons.org/licenses/by/4.0/).
> 
> You are free to use this database in your application, provided you give attribution to DB-IP.com for the data.
> 
> In the case of a web application, you must include a link back to DB-IP.com on pages that display or use results from the database. You may do it by pasting the HTML code snippet below into your code :
>
> `<a href='https://db-ip.com'>IP Geolocation by DB-IP</a>`

## :books: Installation

The program itself is standalone and does not require any installation. However, you will first need to create the database on your SQL server so that the data has a place to be inserted to.

First, create a database, either using SQL Server Management Studio, or using a query similar to the one below (adjusting the data and log file paths as necessary).

```sql
USE [master]
GO
/****** Object:  Database [DbIpLocation] ******/
CREATE DATABASE [DbIpLocation]
 CONTAINMENT = NONE
 ON  PRIMARY 
( NAME = N'DbIpLocation', FILENAME = N'C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\DATA\DbIpLocation.mdf' , SIZE = 3145728KB , MAXSIZE = UNLIMITED, FILEGROWTH = 32768KB )
 LOG ON 
( NAME = N'DbIpLocation_log', FILENAME = N'C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\DATA\DbIpLocation_log.ldf' , SIZE = 16384KB , MAXSIZE = UNLIMITED , FILEGROWTH = 16384KB )
 WITH CATALOG_COLLATION = DATABASE_DEFAULT, LEDGER = OFF
GO
ALTER DATABASE [DbIpLocation] SET RECOVERY SIMPLE
GO
```

Next, run the SQL script here: [/sql/Database Creation Query.sql](/sql/Database%20Creation%20Query.sql)

This creates the following tables:

- `tblCountries` which is provided separately and is not part of the DB-IP database. This is used to convert a two-character country code into the full country's name.
- `tblDataVersion` used to keep track of the version (month/year) of the data stored in the database. This is checked when the program runs, so that no action will be taken if the version on the website is the same version already stored in the database.

A 3rd table, `tblIpAddresses` which is used to actually store the IP-to-location data, will be created in the database during run time of the application. The database creation query linked above does contain the SQL for this table for reference, but it will be created/replaced during run time if present.

Next you should configure the connection string to the database so that the application knows where to insert the data, explained in the next section.

## :wrench: Configuration

Configuration is done using the `appsettings.json` file in the application folder and looks like this:

```json
{
  "AppSettings": {
    "RecordsPerUpload": 50000,
    "ConnectionStrings": {
      "DbIpLocation": "Data Source=localhost;Initial Catalog=DbIpLocation;User ID=username;Password=password;Encrypt=false"
    }
  }
}

```

As shown above, the configuration has the following two settings:

- **RecordsPerUpload:** The application will read this many rows from the file, then insert them to the database before reading more rows. The application may run slightly faster with a larger number but will use more memory, due to the fact that there would be a larger number of rows to hold in memory before inserting to the database. You could also reduce this number to cause the application to use less memory. For reference, the total database size is over *5.5 million records*.
- **ConnectionStrings.DbIpLocation**: This is the connection string used to connect to your database. You should update this to match your database, where `Data Source` is the hostname of the server and `Initial Catalog` is the name of the database.

## :alarm_clock: Running the application on a schedule

As mentioned earlier, the data is only downloaded if the database on the DB-IP website is newer than the version stored in the `tblDataVersion` table. This means it is safe to run the application on a schedule to keep the data up to date.

Data is also first inserted into a temporary table during processing. Then, using a transaction, the current `tblIpAddresses` table is dropped, and the temporary table renamed to `tblIpAddresses` to then be used as the live table. This means that applications using the database should not experience any downtime while the update is being processed.

## :mag: Querying the database to lookup the location of an IP address

To query the database and find the location for a given IP address, the IP address should first be converted to bytes using the built-in function `System.Net.IPAddress.GetAddressBytes()`. You can then query the `tblIpAddresses` database table for the row where the IP's bytes are between the values of the `StartIpAddressBytes` and `EndIpAddressBytes` columns.

Here's a sample class which could be used to do that. This class should be registered as a singleton in the application's services/dependency injection.

### Program.cs

*Note: The use of `builder.Services` assumes the code is being used in an ASP.NET Core application using .NET 6 or above using the [minimal hosting model](https://learn.microsoft.com/en-us/aspnet/core/migration/50-to-60?view=aspnetcore-7.0&tabs=visual-studio#new-hosting-model).*

```
builder.Services.AddSingleton<DbIpLocationRepository>();
```

### DbIpLocationRepository.cs

*Note: You may need to adjust the use of `AppSettings` or the connection string below to suit your project. This example uses the `Dapper` nuget package for querying the database.*

```csharp
using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Net;
using System.Net.Sockets;

namespace MyApplication
{
    internal sealed class DbIpLocationRepository
    {
        private readonly AppSettings _appSettings;

        public Application(IOptions<AppSettings> appSettings)
        {
            _appSettings = appSettings.Value;
        }
    
        public async Task<string> GetLocationStringForIpAddressAsync(string ipAddress, CancellationToken cancellationToken = default)
        {
            (int ipVersion, byte[]? ipAddressBytes) = IpAddressToBytes(ipAddress);

            if (ipVersion == 0 || ipAddressBytes is null)
            {
                return "";
            }

            using (SqlConnection sqlConnection = new SqlConnection(_appSettings.ConnectionStrings.DbIpLocation))
            {
                string sql = @"
select tblCountries.CountryName
      ,tblIpAddresses.Region
      ,tblIpAddresses.City
from DbIpLocation.dbo.tblIpAddresses
left join DbIpLocation.dbo.tblCountries
on tblIpAddresses.Country = tblCountries.CountryCode
where @ipAddressBytes between tblIpAddresses.StartIpAddressBytes and tblIpAddresses.EndIpAddressBytes
and tblIpAddresses.AddressFamily = @addressFamily
";
                DynamicParameters parameters = new DynamicParameters();
                parameters.Add("@addressFamily", ipVersion, DbType.Byte, ParameterDirection.Input);
                parameters.Add("@ipAddressBytes", ipAddressBytes, DbType.Binary, ParameterDirection.Input, 16);

                CommandDefinition commandDefinition = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);

                (string countryName, string region, string city) = await sqlConnection.QueryFirstOrDefaultAsync<(string, string, string)>(commandDefinition);

                string result = "";

                // Append city
                if (!string.IsNullOrEmpty(city))
                {
                    result = city;
                }

                // Append region
                if (!string.IsNullOrEmpty(region))
                {
                    if (result != "")
                    {
                        result += ", ";
                    }

                    result += region;
                }

                // Append country name
                if (!string.IsNullOrEmpty(countryName))
                {
                    if (result != "")
                    {
                        result += ", ";
                    }

                    result += countryName;
                }

                return result;
            }
        }
        
        /// <summary>
        /// <para>Takes an input IPv4 or IPv6 IP address as a string. Returns the detected version (4 or 6) as well as a representation of the IP address in bytes.</para>
        /// <para>If the IP address failed to be parsed, returns (0, null).</para>
        /// </summary>
        /// <param name="ipAddress">The input IPv4 or IPv6 IP address to convert to bytes.</param>
        /// <returns></returns>
        public static (int ipVersion, byte[]? ipAddressBytes) IpAddressToBytes(string ipAddress)
        {
            if (IPAddress.TryParse(ipAddress, out IPAddress? parsedIpAddress))
            {
                int addressFamilyInt = 0;

                if (parsedIpAddress.AddressFamily == AddressFamily.InterNetwork)
                {
                    addressFamilyInt = 4;
                }
                else if (parsedIpAddress.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    addressFamilyInt = 6;
                }

                return (addressFamilyInt, parsedIpAddress.GetAddressBytes());
            }

            return (0, null);
        }
    }
}
```
