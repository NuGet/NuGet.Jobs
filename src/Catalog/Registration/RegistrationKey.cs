﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Registration
{
    public class RegistrationKey
    {
        public RegistrationKey(string id)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
        }

        public string Id { get; }

        public override string ToString()
        {
            return Id.ToLowerInvariant();
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        public override bool Equals(object obj)
        {
            RegistrationKey rhs = obj as RegistrationKey;

            if (rhs == null)
            {
                return false;
            }

            return StringComparer.OrdinalIgnoreCase.Equals(Id, rhs.Id);
        }

        public static RegistrationKey Promote(string resourceUri, IGraph graph)
        {
            INode subject = graph.CreateUriNode(new Uri(resourceUri));
            string id = graph.GetTriplesWithSubjectPredicate(subject, graph.CreateUriNode(Schema.Predicates.Id)).First().Object.ToString();

            return new RegistrationKey(id);
        }
    }
}