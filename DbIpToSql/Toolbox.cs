using Microsoft.Data.SqlClient;
using System.Collections;
using System.Data.Common;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DbIpToSql
{
    internal static class Toolbox
    {
        /// <summary>
        /// <para>Returns a formatted string containing details for a single Exception, intended to be used for writing to a log or sending an error email notification.</para>
        /// <para>This does not include InnerExceptions. For all InnerExceptions please use <see cref="GetExceptionString(Exception)"/> instead.</para>
        /// <para>For WebExceptions, it will also retrieve and include the web response content. For SqlExceptions, for each SQL error it will also include query line numbers and details from the database.</para>
        /// </summary>
        /// <param name="exception">The Exception to parse details for.</param>
        /// <returns></returns>
        public static string GetExceptionStringSingle(Exception exception)
        {
            StringBuilder errorStr = new StringBuilder();

            string newLine = Environment.NewLine;
            string twoNewLines = Environment.NewLine + Environment.NewLine;

            errorStr.Append(exception.Source);
            errorStr.Append(twoNewLines);
            errorStr.Append(exception.GetType().ToString() + ": " + exception.Message);
            errorStr.Append(twoNewLines);
            errorStr.Append(exception.StackTrace);

            string webResponse = string.Empty;

            // If Exception is a WebException, get the response data
            // and display it on the email.
            if (exception.GetType() == typeof(WebException))
            {
                WebException webEx = (WebException)exception;

                if (webEx.Response != null)
                {
                    using (Stream responseStream = webEx.Response.GetResponseStream())
                    {
                        if (responseStream != null)
                        {
                            try
                            {
                                webResponse = new StreamReader(webEx.Response.GetResponseStream()).ReadToEnd();
                            }
                            catch (Exception e)
                            {
                                webResponse = "*** Exception occurred while reading response content:" + twoNewLines + GetExceptionString(e);
                            }

                            if (!string.IsNullOrEmpty(webResponse))
                            {
                                errorStr.Append(twoNewLines + "Web Response content:" + twoNewLines);
                                errorStr.Append(webResponse + twoNewLines);
                            }
                        }
                    }
                }

                if (webResponse == "")
                {
                    errorStr.Append(twoNewLines + "There was no content in the body of the Web Response." + twoNewLines);
                }
            }
            // If Exception is a SqlException, get additional info.
            else if (exception.GetType() == typeof(SqlException))
            {
                SqlException sqlEx = (SqlException)exception;
                errorStr.Append(twoNewLines + "SQL Exception details:");

                if (sqlEx.Errors.Count == 0)
                {
                    errorStr.Append(twoNewLines + "  No additional information.");
                }
                else
                {
                    for (int i = 0; i < sqlEx.Errors.Count; i++)
                    {
                        errorStr.Append(twoNewLines + "  SQL Error #" + (i + 1) + " of " + sqlEx.Errors.Count + newLine +
                            "  Message: " + sqlEx.Errors[i].Message + newLine +
                            "  Error Number: " + sqlEx.Errors[i].Number + newLine +
                            "  Line Number: " + sqlEx.Errors[i].LineNumber + newLine +
                            "  Source: " + sqlEx.Errors[i].Source + newLine +
                            "  Procedure: " + sqlEx.Errors[i].Procedure);
                    }
                }
            }
            // If Exception is a DbException or derived from DbException, get additional info.
            else if (exception.GetType() == typeof(DbException) || exception.GetType().IsSubclassOf(typeof(DbException)))
            {
                DbException dbEx = (DbException)exception;
                errorStr.Append(twoNewLines + "DB Exception details:");

                errorStr.AppendLine(twoNewLines + "  Base Error Code: " + dbEx.ErrorCode);

                foreach (DictionaryEntry entry in dbEx.Data)
                {
                    errorStr.AppendLine("  " + entry.Key + ": " + entry.Value);
                }
            }

            return errorStr.ToString();
        }

        /// <summary>
        /// <para>Returns a formatted string containing details for an Exception and all its InnerExceptions, intended to be used for writing to a log or sending an error email notification.</para>
        /// <para>For WebExceptions, it will also retrieve the web response content. For SqlExceptions, for each SQL error it will also include query line numbers and details from the database.</para>
        /// </summary>
        /// <param name="exception">The Exception to parse details for.</param>
        /// <returns></returns>
        public static string GetExceptionString(Exception exception)
        {
            StringBuilder errorStr = new StringBuilder();

            string twoNewLines = Environment.NewLine + Environment.NewLine;

            // Parse exception details
            errorStr.Append(GetExceptionStringSingle(exception));

            // Loop through all inner exceptions
            Exception? inner = exception.InnerException;

            while (inner != null)
            {
                errorStr.Append(twoNewLines + "InnerException:" + twoNewLines);

                // Parse inner exception details
                errorStr.Append(GetExceptionStringSingle(inner));

                // Get next inner exception
                inner = inner.InnerException;
            }

            // Return full message
            return errorStr.ToString();
        }

        /// <summary>
        /// <para>Takes an input IPv4 or IPv6 IP address. Returns the detected version as well as a representation of the IP address in bytes.</para>
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

        /// <summary>
        /// <para>Returns the path to the executable (without trailing \)</para>
        /// </summary>
        /// <returns></returns>
        public static string GetExecutableDirectory()
        {
            return Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory)!;
        }
    }
}
