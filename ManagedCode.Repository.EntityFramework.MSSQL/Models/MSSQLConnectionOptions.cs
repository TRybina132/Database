﻿using System.Reflection;

namespace ManagedCode.Repository.EntityFramework.MSSQL.Models
{
    public class MSSQLConnectionOptions
    {
        public string ConnectionString { get; set; }
        public bool UseTracking { get; set; }
    }
}