// MIT License
// Copyright (c) 2019 litetex
// https://gist.github.com/litetex/b88fe0531e5acea82df1189643fb1f79

using System.Runtime.CompilerServices;

namespace DbIpToSql
{
    public static class Log
    {
        private static string FormatForException(this string message, Exception ex)
        {
            return $"{message}: {(ex != null ? ex.ToString() : "")}";
        }

        private static string FormatForContext(this string message, [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            var fileName = Path.GetFileNameWithoutExtension(sourceFilePath);
            var methodName = memberName;

            return @$"{fileName} [{methodName}]{(sourceLineNumber > 0 ? ":" + sourceLineNumber.ToString() : "")}
{message}";
        }

        public static void Verbose(string message, [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            Serilog.Log.Verbose(
               message
               .FormatForContext(memberName, sourceFilePath, sourceLineNumber)
               );
        }

        public static void Verbose(string message, Exception ex, [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            Serilog.Log.Verbose(
               message
               .FormatForException(ex)
               .FormatForContext(memberName, sourceFilePath, sourceLineNumber)
               );
        }

        public static void Debug(string message, [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            Serilog.Log.Debug(message
               .FormatForContext(memberName, sourceFilePath, sourceLineNumber)
               );
        }

        public static void Debug(string message, Exception ex, [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            Serilog.Log.Debug(
               message
               .FormatForException(ex)
               .FormatForContext(memberName, sourceFilePath, sourceLineNumber)
               );
        }

        public static void Information(string message, [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            Serilog.Log.Information(
               message
               .FormatForContext(memberName, sourceFilePath, sourceLineNumber)
               );
        }

        public static void Information(string message, Exception ex, [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {

            Serilog.Log.Information(
               message
               .FormatForException(ex)
               .FormatForContext(memberName, sourceFilePath, sourceLineNumber));
        }

        public static void Warn(string message, [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {

            Serilog.Log.Warning(
               message
               .FormatForContext(memberName, sourceFilePath, sourceLineNumber));
        }

        public static void Warn(string message, Exception ex, [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {

            Serilog.Log.Warning(
               message
               .FormatForException(ex)
               .FormatForContext(memberName, sourceFilePath, sourceLineNumber));
        }

        public static void Error(string message, [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {

            Serilog.Log.Error(
               message
               .FormatForContext(memberName, sourceFilePath, sourceLineNumber));
        }

        public static void Error(string message, Exception ex, [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {

            Serilog.Log.Error(
               message
               .FormatForException(ex)
               .FormatForContext(memberName, sourceFilePath, sourceLineNumber));
        }

        public static void Error(Exception ex, [CallerMemberName]
          string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {

            Serilog.Log.Error(
               (ex != null ? ex.ToString() : "")
               .FormatForContext(memberName, sourceFilePath, sourceLineNumber));
        }

        public static void Fatal(string message, [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            FatalAction();

            Serilog.Log.Error(
               message
               .FormatForContext(memberName, sourceFilePath, sourceLineNumber));
        }

        public static void Fatal(string message, Exception ex, [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            FatalAction();

            Serilog.Log.Error(
               message
               .FormatForException(ex)
               .FormatForContext(memberName, sourceFilePath, sourceLineNumber));
        }

        public static void Fatal(Exception ex, [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            FatalAction();

            Serilog.Log.Error(
               ex.ToString()
               .FormatForContext(memberName, sourceFilePath, sourceLineNumber));
        }

        private static void FatalAction()
        {
            Environment.ExitCode = -1;
        }
    }
}
