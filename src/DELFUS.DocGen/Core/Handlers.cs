using Dapper;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;

namespace DELFUS.DocGen
{
    /// <summary>
    /// Acts on behalf of all type-handlers in this package
    /// </summary>
    public static class Handlers
    {
        /// <summary>
        /// Register all type-handlers in this package
        /// </summary>
        public static void Register()
        {
            SqlMapper.AddTypeHandler(DateTimeHandler.Default);
            //SqlMapper.AddTypeHandler(NullableDateTimeHandler.Default);

            //NOTE: Default Command Timeout
            SqlMapper.Settings.CommandTimeout = 120;
        }

        public static IConfigurationRoot Config()
        {
            AppDomain currentDomain = AppDomain.CurrentDomain;
            //currentDomain.UnhandledException += new UnhandledExceptionEventHandler(MyHandler);
            var devEnvironmentVariable = Environment.GetEnvironmentVariable("NETCORE_ENVIRONMENT");
            var isDevelopment = string.IsNullOrEmpty(devEnvironmentVariable) || devEnvironmentVariable.ToLower() == "development";

            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables();

            return builder.Build();
        }

    }

    /// <summary>
    /// Type-handler for the DateTime type.
    /// </summary>
    public class DateTimeHandler : SqlMapper.TypeHandler<DateTime>
    {
        /// <summary>
        /// Create a new handler instance.
        /// </summary>
        protected DateTimeHandler() { /* create new */ }

        /// <summary>
        /// Default handler instance.
        /// </summary>
        public static readonly DateTimeHandler Default = new DateTimeHandler();

        /// <summary>
        /// Assign the value of a parameter before a command executes.
        /// </summary>
        /// <param name="parameter">The parameter to configure.</param>
        /// <param name="value">Parameter value.</param>
        public override void SetValue(IDbDataParameter parameter, DateTime value)
        {
            object adjusted;

            if (value == DateTime.MinValue)
            {
                adjusted = null;
            }
            // If it is local time, convert it to UTC
            else if (value.Kind == DateTimeKind.Local)
            {
                adjusted = value.ToUniversalTime();
            }
            else
            {
                adjusted = value;
            }

            parameter.Value = adjusted ?? DBNull.Value;
        }

        /// <summary>
        /// Parse a database value back to a typed value.
        /// </summary>
        /// <param name="value">The value from the database.</param>
        /// <returns>The typed value.</returns>
        public override DateTime Parse(object value)
        {
            if (value == null || value is DBNull) return DateTime.MinValue;

            return DateTime.SpecifyKind((DateTime)value, DateTimeKind.Utc);
        }
    }


    /// <summary>
    /// Type-handler for the DateTime type.
    /// </summary>
    public class NullableDateTimeHandler : SqlMapper.TypeHandler<DateTime?>
    {
        /// <summary>
        /// Create a new handler instance.
        /// </summary>
        protected NullableDateTimeHandler() { /* create new */ }

        /// <summary>
        /// Default handler instance.
        /// </summary>
        public static readonly NullableDateTimeHandler Default = new NullableDateTimeHandler();

        /// <summary>
        /// Assign the value of a parameter before a command executes.
        /// </summary>
        /// <param name="parameter">The parameter to configure.</param>
        /// <param name="value">Parameter value.</param>
        public override void SetValue(IDbDataParameter parameter, DateTime? value)
        {
            object adjusted;

            if (!value.HasValue || value.Value == DateTime.MinValue)
            {
                adjusted = null;
            }
            // If it is local time, convert it to UTC
            else if (value.Value.Kind == DateTimeKind.Local)
            {
                adjusted = value.Value.ToUniversalTime();
            }
            else
            {
                adjusted = value.Value;
            }

            parameter.Value = adjusted ?? DBNull.Value;
        }

        /// <summary>
        /// Parse a database value back to a typed value.
        /// </summary>
        /// <param name="value">The value from the database.</param>
        /// <returns>The typed value.</returns>
        public override DateTime? Parse(object value)
        {
            if (value == null || value is DBNull) return DateTime.MinValue;

            return DateTime.SpecifyKind((DateTime)value, DateTimeKind.Utc);
        }
    }
}
