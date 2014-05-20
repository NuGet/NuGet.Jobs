﻿using System;

namespace NuGet.Services.Metadata.Catalog.Maintenance
{
    public abstract class CatalogItem
    {
        DateTime _timeStamp;
        string _baseAddress;

        public void SetTimeStamp(DateTime timeStamp)
        {
            _timeStamp = timeStamp;
        }

        public void SetBaseAddress(string baseAddress)
        {
            _baseAddress = baseAddress;
        }

        public abstract string CreateContent(CatalogContext context);

        public abstract string GetItemType();

        protected abstract string GetItemName();

        public string GetBaseAddress()
        {
            return _baseAddress + "catalog/item/" + MakeTimeStampPathComponent(_timeStamp);
        }

        public string GetRelativeAddress()
        {
            return GetItemName() + ".json";
        }

        protected static string MakeTimeStampPathComponent(DateTime timeStamp)
        {
            return string.Format("{0}.{1}.{2}.{3}.{4}.{5}/", timeStamp.Year, timeStamp.Month, timeStamp.Day, timeStamp.Hour, timeStamp.Minute, timeStamp.Second);
        }
    }
}
