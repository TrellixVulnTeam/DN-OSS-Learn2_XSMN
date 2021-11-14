﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Azure.Identity;
using Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;
using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    public class AKVTest : IClassFixture<SQLSetupStrategyAzureKeyVault>
    {
        private SQLSetupStrategyAzureKeyVault fixture;
        private readonly string akvTableName;

        public AKVTest(SQLSetupStrategyAzureKeyVault fixture)
        {
            this.fixture = fixture;
            akvTableName = fixture.AKVTestTable.Name;

            // Disable the cache to avoid false failures.
            SqlConnection.ColumnEncryptionQueryMetadataCacheEnabled = false;
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void TestEncryptDecryptWithAKV()
        {
            using (SqlConnection sqlConnection = new SqlConnection(string.Concat(DataTestUtility.TCPConnectionString, @";Column Encryption Setting = Enabled;")))
            {
                sqlConnection.Open();

                Customer customer = new Customer(45, "Microsoft", "Corporation");

                // Start a transaction and either commit or rollback based on the test variation.
                using (SqlTransaction sqlTransaction = sqlConnection.BeginTransaction())
                {
                    DatabaseHelper.InsertCustomerData(sqlConnection, sqlTransaction, akvTableName, customer);
                    sqlTransaction.Commit();
                }

                // Test INPUT parameter on an encrypted parameter
                using SqlCommand sqlCommand = new SqlCommand($"SELECT CustomerId, FirstName, LastName FROM [{akvTableName}] WHERE FirstName = @firstName",
                                                                sqlConnection);
                SqlParameter customerFirstParam = sqlCommand.Parameters.AddWithValue(@"firstName", @"Microsoft");
                customerFirstParam.Direction = System.Data.ParameterDirection.Input;
                customerFirstParam.ForceColumnEncryption = true;

                using SqlDataReader sqlDataReader = sqlCommand.ExecuteReader();
                DatabaseHelper.ValidateResultSet(sqlDataReader);
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsAKVSetupAvailable))]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestRoundTripWithAKVAndCertStoreProvider()
        {
            using (SQLSetupStrategyCertStoreProvider certStoreFixture = new SQLSetupStrategyCertStoreProvider())
            {
                byte[] plainTextColumnEncryptionKey = ColumnEncryptionKey.GenerateRandomBytes(ColumnEncryptionKey.KeySizeInBytes);
                byte[] encryptedColumnEncryptionKeyUsingAKV = fixture.AkvStoreProvider.EncryptColumnEncryptionKey(DataTestUtility.AKVUrl, @"RSA_OAEP", plainTextColumnEncryptionKey);
                byte[] columnEncryptionKeyReturnedAKV2Cert = certStoreFixture.CertStoreProvider.DecryptColumnEncryptionKey(certStoreFixture.CspColumnMasterKey.KeyPath, @"RSA_OAEP", encryptedColumnEncryptionKeyUsingAKV);
                Assert.True(plainTextColumnEncryptionKey.SequenceEqual(columnEncryptionKeyReturnedAKV2Cert), @"Roundtrip failed");

                // Try the opposite.
                byte[] encryptedColumnEncryptionKeyUsingCert = certStoreFixture.CertStoreProvider.EncryptColumnEncryptionKey(certStoreFixture.CspColumnMasterKey.KeyPath, @"RSA_OAEP", plainTextColumnEncryptionKey);
                byte[] columnEncryptionKeyReturnedCert2AKV = fixture.AkvStoreProvider.DecryptColumnEncryptionKey(DataTestUtility.AKVUrl, @"RSA_OAEP", encryptedColumnEncryptionKeyUsingCert);
                Assert.True(plainTextColumnEncryptionKey.SequenceEqual(columnEncryptionKeyReturnedCert2AKV), @"Roundtrip failed");
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void TestLocalCekCacheIsScopedToProvider()
        {
            using (SqlConnection sqlConnection = new(string.Concat(DataTestUtility.TCPConnectionString, @";Column Encryption Setting = Enabled;")))
            {
                sqlConnection.Open();

                Customer customer = new(45, "Microsoft", "Corporation");

                // Test INPUT parameter on an encrypted parameter
                using (SqlCommand sqlCommand = new($"SELECT CustomerId, FirstName, LastName FROM [{akvTableName}] WHERE FirstName = @firstName",
                                                                sqlConnection))
                {
                    SqlParameter customerFirstParam = sqlCommand.Parameters.AddWithValue(@"firstName", @"Microsoft");
                    customerFirstParam.Direction = System.Data.ParameterDirection.Input;
                    customerFirstParam.ForceColumnEncryption = true;

                    SqlDataReader sqlDataReader = sqlCommand.ExecuteReader();
                    sqlDataReader.Close();

                    SqlColumnEncryptionAzureKeyVaultProvider sqlColumnEncryptionAzureKeyVaultProvider =
                        new(new SqlClientCustomTokenCredential());

                    Dictionary<string, SqlColumnEncryptionKeyStoreProvider> customProvider = new()
                    {
                        { SqlColumnEncryptionAzureKeyVaultProvider.ProviderName, sqlColumnEncryptionAzureKeyVaultProvider }
                    };

                    // execute a query using provider from command-level cache. this will cache the cek in the local cek cache
                    sqlCommand.RegisterColumnEncryptionKeyStoreProvidersOnCommand(customProvider);
                    SqlDataReader sqlDataReader2 = sqlCommand.ExecuteReader();
                    sqlDataReader2.Close();

                    // global cek cache and local cek cache are populated above
                    // when using a new per-command provider, it will only use its local cek cache 
                    // the following query should fail due to an empty cek cache and invalid credentials
                    customProvider[SqlColumnEncryptionAzureKeyVaultProvider.ProviderName] =
                        new SqlColumnEncryptionAzureKeyVaultProvider(new ClientSecretCredential("tenant", "client", "secret"));
                    sqlCommand.RegisterColumnEncryptionKeyStoreProvidersOnCommand(customProvider);
                    Exception ex = Assert.Throws<SqlException>(() => sqlCommand.ExecuteReader());
                    Assert.Contains("ClientSecretCredential authentication failed", ex.Message);
                }
            }
        }

    }
}
