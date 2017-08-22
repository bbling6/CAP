﻿using Xunit;
using Dapper;

namespace DotNetCore.CAP.PostgreSql.Test
{
    //[Collection("postgresql")]
    public class SqlServerStorageTest : DatabaseTestHost
    {
        private readonly string _dbName;
        private readonly string _masterDbConnectionString;

        public SqlServerStorageTest()
        {
            _dbName = ConnectionUtil.GetDatabaseName();
            _masterDbConnectionString = ConnectionUtil.GetMasterConnectionString();
        }

        //[Fact]
        public void Database_IsExists()
        {
            using (var connection = ConnectionUtil.CreateConnection(_masterDbConnectionString))
            {
                var databaseName = ConnectionUtil.GetDatabaseName();
                var sql = $@"SELECT SCHEMA_NAME FROM SCHEMATA WHERE SCHEMA_NAME = '{databaseName}'";
                var result = connection.QueryFirstOrDefault<string>(sql);
                Assert.NotNull(result);
                Assert.True(databaseName.Equals(result, System.StringComparison.CurrentCultureIgnoreCase));
            }
        }

        //[Fact]
        public void DatabaseTable_Published_IsExists()
        {
            var tableName = "cap.published";
            using (var connection = ConnectionUtil.CreateConnection(_masterDbConnectionString))
            {
                var sql = $"SELECT TABLE_NAME FROM `TABLES` WHERE TABLE_SCHEMA='{_dbName}' AND TABLE_NAME = '{tableName}'";
                var result = connection.QueryFirstOrDefault<string>(sql);
                Assert.NotNull(result);
                Assert.Equal(tableName, result);
            }
        }

        //[Fact]
        public void DatabaseTable_Queue_IsExists()
        {
            var tableName = "cap.queue";
            using (var connection = ConnectionUtil.CreateConnection(_masterDbConnectionString))
            {
                var sql = $"SELECT TABLE_NAME FROM `TABLES` WHERE TABLE_SCHEMA='{_dbName}' AND TABLE_NAME = '{tableName}'";
                var result = connection.QueryFirstOrDefault<string>(sql);
                Assert.NotNull(result);
                Assert.Equal(tableName, result);
            }
        }

        //[Fact]
        public void DatabaseTable_Received_IsExists()
        {
            var tableName = "cap.received";
            using (var connection = ConnectionUtil.CreateConnection(_masterDbConnectionString))
            {
                var sql = $"SELECT TABLE_NAME FROM `TABLES` WHERE TABLE_SCHEMA='{_dbName}' AND TABLE_NAME = '{tableName}'";
                var result = connection.QueryFirstOrDefault<string>(sql);
                Assert.NotNull(result);
                Assert.Equal(tableName, result);
            }
        }
    }
}