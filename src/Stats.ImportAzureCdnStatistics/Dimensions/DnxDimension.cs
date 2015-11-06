// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Stats.ImportAzureCdnStatistics
{
    public class DnxDimension
    {
        public DnxDimension(string dnxVersion, string operatingSystem, string fileName)
        {
            DnxVersion = dnxVersion;
            OperatingSystem = operatingSystem;
            FileName = fileName;
        }

        public int Id { get; set; }
        public string DnxVersion { get; }
        public string OperatingSystem { get; }
        public string FileName { get; }

        protected bool Equals(DnxDimension other)
        {
            return string.Equals(DnxVersion, other.DnxVersion) && string.Equals(OperatingSystem, other.OperatingSystem) && string.Equals(FileName, other.FileName);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((DnxDimension)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (DnxVersion != null ? DnxVersion.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (OperatingSystem != null ? OperatingSystem.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (FileName != null ? FileName.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}