namespace RotateSecrets
{
    /// <summary>
    /// WARNING: Changing the value of these strings WILL require changes to the tags stored on KeyVault!
    /// </summary>
    public static class Tags
    {
        /// <summary>
        /// If "True", the job will rotate the secret.
        /// </summary>
        public const string ShouldRotate = "ShouldRotate";

        /// <summary>
        /// Describes the type of secret it is.
        /// See other tags with a "Type-" prefix, such as TypeSqlAccount.
        /// </summary>
        public const string Type = "Type";
        
        #region SQL Account Tags
        public const string TypeSqlAccount = "SqlAccount";

        /// <summary>
        /// The name of the primary user secret that this Sql account secret is associated with.
        /// </summary>
        public const string SqlPrimaryUserSecretName = "SqlPrimaryUserSecretName";

        /// <summary>
        /// The type of Sql credential described by this secret.
        /// See other tags with a "SqlType-" prefix, such as SqlTypeUser.
        /// </summary>
        public const string SqlType = "SqlType";
        public const string SqlTypeUser = "SqlUsername";
        public const string SqlTypePassword = "SqlPassword";

        /// <summary>
        /// The rank of the Sql credential described by this secret.
        /// See other tags with a "SqlRank-" prefix, such as SqlRankPrimary.
        /// </summary>
        public const string SqlRank = "SqlRank";
        public const string SqlRankPrimary = "Primary";
        public const string SqlRankSecondary = "Secondary";

        /// <summary>
        /// The Sql server that this Sql account secret is associated with.
        /// </summary>
        public const string SqlServerUrl = "SqlServerUrl";

        /// <summary>
        /// The name of the database that this Sql account secret is associated with.
        /// </summary>
        public const string SqlDatabaseName = "SqlDatabaseName";
        #endregion
        
        #region Storage Account Access Key Tags
        public const string TypeStorageAccountAccessKey = "StorageAccountAccessKey";

        /// <summary>
        /// The resource group in which the storage account that this access key is associated with.
        /// </summary>
        public const string StorageAccountResourceGroup = "StorageAccountResourceGroup";

        /// <summary>
        /// The name of the storage account that this access key secret is associated with.
        /// </summary>
        public const string StorageAccountName = "StorageAccountName";
        #endregion
    }
}
