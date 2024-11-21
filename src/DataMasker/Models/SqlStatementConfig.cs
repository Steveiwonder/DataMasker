using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;

namespace DataMasker.Models
{
    /// <summary>
    /// TableConfig
    /// </summary>
    public class SqlStatementConfig
    {
        /// <summary>
        /// The name of the sql statement
        /// </summary>
        /// <value>
        /// The name.
        /// </value>
        [JsonRequired]
        public string Name { get; set; }

        /// <summary>
        /// The sql statement to be executed angainst db
        /// </summary>
        /// <value>
        /// The sql statement
        /// </value>
        [JsonRequired]
        public string Statement { get; set; }

    }
}
