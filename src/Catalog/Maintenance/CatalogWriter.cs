﻿using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Maintenance
{
    public class CatalogWriter : IDisposable
    {
        Storage _storage;
        List<CatalogItem> _batch;
        CatalogContext _context;
        bool _append;
        bool _first;
        bool _open;

        public CatalogWriter(Storage storage, CatalogContext context, int maxPageSize = 1000, bool append = true)
        {
            Options.InternUris = false;

            _storage = storage;
            _context = context;
            _append = append;
            _batch = new List<CatalogItem>();
            MaxPageSize = maxPageSize;
            _first = true;
            _open = true;
        }

        public int MaxPageSize
        {
            get;
            private set;
        }

        public void Add(CatalogItem item)
        {
            Check();

            _batch.Add(item);
        }

        public async Task Commit(DateTime timeStamp)
        {
            Check();

            if (_batch.Count == 0)
            {
                return;
            }

            string baseAddress = string.Format("{0}{1}/", _storage.BaseAddress, _storage.Container);

            IDictionary<Uri, string> pageItems = new Dictionary<Uri, string>();
            List<Task> tasks = null;
            foreach (CatalogItem item in _batch)
            {
                item.SetTimeStamp(timeStamp);
                item.SetBaseAddress(baseAddress);

                Uri resourceUri = new Uri(item.GetBaseAddress() + item.GetRelativeAddress());
                string content = item.CreateContent(_context);

                if (content != null)
                {
                    if (tasks == null)
                    {
                        tasks = new List<Task>();
                    }
                    tasks.Add(_storage.Save("application/json", resourceUri, content));
                }

                pageItems.Add(resourceUri, item.GetItemType());
            }

            if (tasks != null)
            {
                await Task.WhenAll(tasks.ToArray());
            }

            Uri rootResourceUri = new Uri(baseAddress + "catalog/index.json");

            string rootContent = null;
            if (!_first || _first && _append)
            {
                rootContent = await _storage.Load(rootResourceUri);
            }
            _first = false;

            CatalogRoot root = new CatalogRoot(rootResourceUri, rootContent);
            CatalogPage page;

            Tuple<Uri, int> latestPage = root.GetLatestPage();

            Uri pageResourceUri;
            if (latestPage == null || latestPage.Item2 + pageItems.Count > MaxPageSize)
            {
                pageResourceUri = root.AddNextPage(timeStamp, pageItems.Count);
                page = new CatalogPage(pageResourceUri, rootResourceUri);
            }
            else
            {
                pageResourceUri = latestPage.Item1;
                string pageContent = await _storage.Load(pageResourceUri);
                page = new CatalogPage(pageResourceUri, rootResourceUri, pageContent);
                root.UpdatePage(pageResourceUri, timeStamp, latestPage.Item2 + pageItems.Count);
            }

            foreach (KeyValuePair<Uri, string> pageItem in pageItems)
            {
                page.Add(pageItem.Key, pageItem.Value, timeStamp);
            }

            await _storage.Save("application/json", pageResourceUri, page.CreateContent(_context));

            await _storage.Save("application/json", rootResourceUri, root.CreateContent(_context));

            _batch.Clear();
        }
 
        public void Dispose()
        {
            _open = false;
        }

        void Check()
        {
            if (!_open)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }
}
