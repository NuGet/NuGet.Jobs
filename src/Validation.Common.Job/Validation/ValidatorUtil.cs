// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;

namespace NuGet.Jobs.Validation
{
    /// <summary>
    /// Validator-related utility methods
    /// </summary>
    public static class ValidatorUtil
    {
        /// <summary>
        /// Checks whether given type has <see cref="ValidatorAliasAttribute"/> attribute.
        /// </summary>
        public static bool HasValidatorNameAttribute(Type type)
            => type.CustomAttributes.Any(a => a.AttributeType == typeof(ValidatorAliasAttribute));

        /// <summary>
        /// Retrieves the value of the <see cref="ValidatorAliasAttribute.Name"/> property set 
        /// for <see cref="ValidatorAliasAttribute"/> set on a specified class.
        /// </summary>
        public static string GetValidatorName(Type type)
            => GetCustomAttribute<ValidatorAliasAttribute>(type).Name;

        private static T GetCustomAttribute<T>(Type type)
            where T : Attribute
            => (T)Attribute.GetCustomAttribute(type, typeof(T));
    }
}
