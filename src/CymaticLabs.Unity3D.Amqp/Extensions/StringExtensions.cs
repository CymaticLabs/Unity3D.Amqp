using System;
using System.Linq;

namespace CymaticLabs.Unity3D.Amqp
{
    public static class StringExtensions
    {
        /// <summary>
        /// Splits a string and removes empty entries by default.
        /// </summary>
        /// <param name="value">The string instance.</param>
        /// <param name="c">The split character.</param>
        /// <returns>The split string with all empties removed.</returns>
        public static string[] SplitClean(this string value, char c)
        {
            return value.Split(new char[] { c }, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Splits a string and removes empty entries by default.
        /// </summary>
        /// <param name="value">The string instance.</param>
        /// <param name="separator">The split character.</param>
        /// <param name="trim">Whether or not to trim entries.</param>
        /// <returns>The split string with all empties removed.</returns>
        public static string[] SplitClean(this string value, char c, bool trim)
        {
            return (from v in value.Split(new char[] { c }, StringSplitOptions.RemoveEmptyEntries)
                    select v != null ? v.Trim() : null).ToArray();
        }

        /// <summary>
        /// Splits a string and removes empty entries by default.
        /// </summary>
        /// <param name="value">The string instance.</param>
        /// <param name="separator">The split string.</param>
        /// <returns>The split string with all empties removed.</returns>
        public static string[] SplitClean(this string value, string separator)
        {
            return value.Split(new string[] { separator }, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Splits a string and removes empty entries by default.
        /// </summary>
        /// <param name="value">The string instance.</param>
        /// <param name="separator">The split string.</param>
        /// <param name="trim">Whether or not to trim entries.</param>
        /// <returns>The split string with all empties removed.</returns>
        public static string[] SplitClean(this string value, string separator, bool trim)
        {
            return (from v in value.Split(new string[] { separator }, StringSplitOptions.RemoveEmptyEntries)
                    select v != null ? v.Trim() : null).ToArray();
        }
    }
}
