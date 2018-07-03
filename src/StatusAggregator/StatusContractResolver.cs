using System;
using System.Collections;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace StatusAggregator
{
    public class StatusContractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            JsonProperty property = base.CreateProperty(member, memberSerialization);

            var propertyType = property.PropertyType;

            if (propertyType == typeof(string))
            {
                property.ShouldSerialize = instance => !string.IsNullOrEmpty((string)instance);
            }

            if (typeof(IEnumerable).IsAssignableFrom(propertyType))
            {
                SetShouldSerializeForIEnumerable(property, member);
            }

            return property;
        }

        private void SetShouldSerializeForIEnumerable(JsonProperty property, MemberInfo member)
        {
            Func<object, object> getValue;

            switch (member.MemberType)
            {
                case MemberTypes.Field:
                    getValue = instance => ((FieldInfo)member).GetValue(instance);
                    break;
                case MemberTypes.Property:
                    getValue = instance => ((PropertyInfo)member).GetValue(instance);
                    break;
                default:
                    return;
            }

            property.ShouldSerialize = instance =>
            {
                var value = (IEnumerable)getValue(instance);

                if (value == null)
                {
                    return false;
                }

                foreach (var obj in value)
                {
                    return true;
                }

                return false;
            };
        }
    }
}
