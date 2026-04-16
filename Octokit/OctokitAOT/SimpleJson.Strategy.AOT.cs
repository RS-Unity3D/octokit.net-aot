using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Octokit;
using Octokit.Internal;
using Octokit.Reflection;

#if USE_AOT_JSON
using RS.SimpleJsonUnity;
#endif

namespace RS.Octokit.AOT
{
#if USE_AOT_JSON
    internal class GitHubJsonSerializerStrategy : RS.SimpleJsonUnity.DefaultJsonSerializationStrategy
    {
        private readonly ConcurrentDictionary<Type, ConcurrentDictionary<object, object>> _cachedEnums
            = new ConcurrentDictionary<Type, ConcurrentDictionary<object, object>>();

        private readonly ConcurrentDictionary<Type, IDictionary<string, PropertyOrField>> _propertiesCache
            = new ConcurrentDictionary<Type, IDictionary<string, PropertyOrField>>();

        private string _activityType;

#if NET20 || NET35
        [ThreadStatic]
        private static HashSet<Type> s_processingTypes;

        private static HashSet<Type> GetProcessingTypes()
        {
            if (s_processingTypes == null)
                s_processingTypes = new HashSet<Type>();
            return s_processingTypes;
        }
#else
        private readonly System.Threading.ThreadLocal<HashSet<Type>> m_processingTypes
            = new System.Threading.ThreadLocal<HashSet<Type>>(() => new HashSet<Type>());

        private HashSet<Type> GetProcessingTypes()
        {
            return m_processingTypes.Value;
        }
#endif

        public GitHubJsonSerializerStrategy() : base()
        {
        }

        public override bool TrySerializeNonPrimitiveObject(object input, out object output)
        {
            if (input == null)
            {
                output = null;
                return false;
            }

            var type = input.GetType();

            if (type.GetTypeInfo().IsPrimitive || input is string)
            {
                output = null;
                return false;
            }

            if (input is Enum e)
            {
                output = e.ToParameter();
                return true;
            }

            try
            {
                if (input is IDictionary dict)
                {
                    var result = new Dictionary<string, object>();
                    foreach (DictionaryEntry entry in dict)
                    {
                        var key = entry.Key?.ToString() ?? "";
                        if (entry.Value == null)
                        {
                            result[key] = null;
                        }
                        else if (entry.Value.GetType().GetTypeInfo().IsPrimitive || entry.Value is string || entry.Value is decimal)
                        {
                            result[key] = entry.Value;
                        }
                        else if (entry.Value is Enum enumVal)
                        {
                            result[key] = enumVal.ToParameter();
                        }
                        else if (TrySerializeNonPrimitiveObject(entry.Value, out var nested))
                        {
                            result[key] = nested;
                        }
                        else
                        {
                            result[key] = entry.Value;
                        }
                    }
                    output = result;
                    return true;
                }

                if (input is IEnumerable enumerable && !(input is string))
                {
                    var list = new List<object>();
                    foreach (var item in enumerable)
                    {
                        if (item == null)
                        {
                            list.Add(null);
                        }
                        else if (item.GetType().GetTypeInfo().IsPrimitive || item is string || item is decimal)
                        {
                            list.Add(item);
                        }
                        else if (item is Enum enumVal)
                        {
                            list.Add(enumVal.ToParameter());
                        }
                        else if (TrySerializeNonPrimitiveObject(item, out var nested))
                        {
                            list.Add(nested);
                        }
                        else
                        {
                            list.Add(item);
                        }
                    }
                    output = list;
                    return true;
                }

                var properties = GetOrBuildProperties(type);
                var obj = new Dictionary<string, object>();

                foreach (var prop in properties.Values.Where(p => p.CanSerialize))
                {
                    var value = prop.GetValue(input);
                    if (value == null)
                        continue;

                    if (value.GetType().GetTypeInfo().IsPrimitive || value is string || value is decimal)
                    {
                        obj[prop.JsonFieldName] = value;
                    }
                    else if (value is Enum enumVal)
                    {
                        obj[prop.JsonFieldName] = enumVal.ToParameter();
                    }
                    else if (value is DateTime dt)
                    {
                        obj[prop.JsonFieldName] = dt.ToString("o");
                    }
                    else if (value is DateTimeOffset dto)
                    {
                        obj[prop.JsonFieldName] = dto.ToString("o");
                    }
                    else if (TrySerializeNonPrimitiveObject(value, out var nested))
                    {
                        obj[prop.JsonFieldName] = nested;
                    }
                    else
                    {
                        obj[prop.JsonFieldName] = value;
                    }
                }

                output = obj;
                return true;
            }
            catch
            {
                output = null;
                return false;
            }
        }

        public override object DeserializeObject(object value, Type type)
        {
            if (value == null)
                return null;

            if (type == null)
                return value;

            if (type == typeof(object))
                return value;

            var typeInfo = type.GetTypeInfo();

            if (typeInfo.IsAssignableFrom(value.GetType().GetTypeInfo()))
                return value;

            Type underlyingType = Nullable.GetUnderlyingType(type);
            if (underlyingType != null)
            {
                type = underlyingType;
                typeInfo = type.GetTypeInfo();
            }

            if (type == typeof(string))
            {
                return value is string s ? s : value?.ToString();
            }

            if (type == typeof(Guid))
            {
                var str = value as string;
                if (str != null)
                {
                    if (str.Length == 0) return default(Guid);
                    return new Guid(str);
                }
                return value;
            }

            if (type == typeof(Uri))
            {
                var str = value as string;
                if (str != null)
                {
                    if (Uri.IsWellFormedUriString(str, UriKind.RelativeOrAbsolute))
                    {
                        Uri.TryCreate(str, UriKind.RelativeOrAbsolute, out var result);
                        return result;
                    }
                }
                return null;
            }

            if (type == typeof(char))
            {
                var str = value as string;
                if (str != null)
                {
                    if (str.Length == 1) return str[0];
                    if (str.Length > 1) return str[0];
                    return default(char);
                }
                return value;
            }

            if (type == typeof(TimeSpan))
            {
                var str = value as string;
                if (str != null)
                {
                    if (long.TryParse(str, out var ticks))
                        return new TimeSpan(ticks);
                    return TimeSpan.Parse(str, CultureInfo.InvariantCulture);
                }
                return value;
            }

            if (type == typeof(DateTime))
            {
                var str = value as string;
                if (str != null)
                {
                    try
                    {
                        return DateTime.ParseExact(str, "yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                    }
                    catch
                    {
                        return DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt)
                            ? dt : default(DateTime);
                    }
                }
                if (value is long longVal)
                    return DateTimeOffset.FromUnixTimeSeconds(longVal).DateTime;
                if (value is double doubleVal)
                    return DateTimeOffset.FromUnixTimeSeconds((long)doubleVal).DateTime;
                return value;
            }

            if (type == typeof(DateTimeOffset))
            {
                var str = value as string;
                if (str != null)
                {
                    try
                    {
                        return DateTimeOffset.ParseExact(str, "yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                    }
                    catch
                    {
                        return DateTimeOffset.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto)
                            ? dto : default(DateTimeOffset);
                    }
                }
                if (value is long longVal)
                    return DateTimeOffset.FromUnixTimeSeconds(longVal);
                if (value is double doubleVal)
                    return DateTimeOffset.FromUnixTimeSeconds((long)doubleVal);
                return value;
            }

            if (typeInfo.IsPrimitive || type == typeof(decimal))
            {
                return ConvertPrimitive(value, type);
            }

            if (typeInfo.IsEnum)
            {
                return DeserializeEnumValue(value, type);
            }

            if (ReflectionUtils.IsStringEnumWrapper(type))
            {
                return DeserializeStringEnum(value, type);
            }

            var stringValue = value as string;
            var dictValue = value as IDictionary<string, object>;
            var arrayValue = value as IList<object>;

            if (dictValue != null)
            {
                return DeserializeFromDictionary(dictValue, type);
            }

            if (arrayValue != null)
            {
                return DeserializeFromArray(arrayValue, type);
            }

            if (value is IDictionary nonGenericDict)
            {
                var genericDict = new Dictionary<string, object>();
                foreach (DictionaryEntry entry in nonGenericDict)
                {
                    genericDict[entry.Key?.ToString() ?? ""] = entry.Value;
                }
                return DeserializeFromDictionary(genericDict, type);
            }

            if (value is IEnumerable enumerable && !(value is string))
            {
                var genericList = new List<object>();
                foreach (var item in enumerable)
                {
                    genericList.Add(item);
                }
                return DeserializeFromArray(genericList, type);
            }

            return value;
        }

        public override void ClearCache()
        {
            base.ClearCache();
            _cachedEnums.Clear();
            _propertiesCache.Clear();
        }

        private IDictionary<string, PropertyOrField> GetOrBuildProperties(Type type)
        {
            return _propertiesCache.GetOrAdd(type, t =>
            {
                return t.GetPropertiesAndFields()
                    .ToDictionary(p => p.JsonFieldName, p => p);
            });
        }

        private object DeserializeStringEnum(object value, Type type)
        {
            try
            {
                var factory = RS.SimpleJsonUnity.SimpleJson.GetRegisteredAotFactory(type);
                if (factory != null)
                {
                    var instance = factory();
                    var stringValue = value as string ?? "null";
                    var valueProp = type.GetProperty("Value");
                    if (valueProp != null)
                        valueProp.SetValue(instance, stringValue, null);
                    return instance;
                }
                return Activator.CreateInstance(type, value ?? "null");
            }
            catch
            {
                return null;
            }
        }

        private object DeserializeEnumValue(object value, Type type)
        {
            if (value is string strValue)
            {
                return DeserializeEnum(strValue, type);
            }

            if (value is long || value is int || value is double
                || value is ulong || value is uint
                || value is short || value is ushort
                || value is byte || value is sbyte
                || value is decimal || value is float)
            {
                return SafeEnumConversion(value, type);
            }

            return DeserializeEnum(value?.ToString() ?? "", type);
        }

        private static object SafeEnumConversion(object value, Type enumType)
        {
            if (value == null || !enumType.IsEnum) return value;

            Type underlyingType = Enum.GetUnderlyingType(enumType);
            long longVal;

            if (value is long l) longVal = l;
            else if (value is int i) longVal = i;
            else if (value is short s) longVal = s;
            else if (value is byte b) longVal = b;
            else if (value is sbyte sb) longVal = sb;
            else if (value is ulong ul) return Enum.ToObject(enumType, ul);
            else if (value is uint ui) return Enum.ToObject(enumType, ui);
            else if (value is ushort us) return Enum.ToObject(enumType, us);
            else if (value is double d) longVal = (long)Math.Truncate(d);
            else if (value is float f) longVal = (long)Math.Truncate(f);
            else if (value is decimal dec) longVal = (long)Math.Truncate(dec);
            else
            {
                try { longVal = Convert.ToInt64(value, CultureInfo.InvariantCulture); }
                catch { return Enum.ToObject(enumType, value); }
            }

            if (underlyingType == typeof(int)) return Enum.ToObject(enumType, (int)longVal);
            if (underlyingType == typeof(long)) return Enum.ToObject(enumType, longVal);
            if (underlyingType == typeof(short)) return Enum.ToObject(enumType, (short)longVal);
            if (underlyingType == typeof(byte)) return Enum.ToObject(enumType, (byte)longVal);
            if (underlyingType == typeof(sbyte)) return Enum.ToObject(enumType, (sbyte)longVal);
            if (underlyingType == typeof(uint)) return Enum.ToObject(enumType, (uint)longVal);
            if (underlyingType == typeof(ulong)) return Enum.ToObject(enumType, (ulong)longVal);
            if (underlyingType == typeof(ushort)) return Enum.ToObject(enumType, (ushort)longVal);

            return Enum.ToObject(enumType, longVal);
        }

        private object DeserializeFromDictionary(IDictionary<string, object> dict, Type type)
        {
            if (type == typeof(Activity) && dict.TryGetValue("type", out var typeObj))
            {
                _activityType = typeObj?.ToString();
            }

            if (type == typeof(ActivityPayload) && !string.IsNullOrEmpty(_activityType))
            {
                type = GetPayloadType(_activityType);
            }

            if (ReflectionUtils.IsStringEnumWrapper(type))
            {
                return DeserializeStringEnum(null, type);
            }

            if (ReflectionUtils.IsTypeDictionary(type))
            {
                Type[] genericArgs = type.GetGenericArguments();
                Type keyType = genericArgs.Length >= 2 ? genericArgs[0] : typeof(string);
                Type valueType = genericArgs.Length >= 2 ? genericArgs[1] : typeof(object);

                IDictionary dictInstance;
                var factory = RS.SimpleJsonUnity.SimpleJson.GetRegisteredAotFactory(type);
                if (factory != null)
                {
                    dictInstance = factory() as IDictionary;
                }
                else
                {
                    try
                    {
                        dictInstance = (IDictionary)Activator.CreateInstance(type);
                    }
                    catch
                    {
                        var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
                        factory = RS.SimpleJsonUnity.SimpleJson.GetRegisteredAotFactory(dictType);
                        if (factory != null)
                        {
                            dictInstance = factory() as IDictionary;
                        }
                        else
                        {
                            dictInstance = (IDictionary)Activator.CreateInstance(dictType);
                        }
                    }
                }

                foreach (var kvp in dict)
                {
                    var dictKey = ConvertDictionaryKey(kvp.Key, keyType);
                    var dictValue = DeserializeObject(kvp.Value, valueType);
                    dictInstance[dictKey] = dictValue;
                }

                return dictInstance;
            }

            var processingTypes = GetProcessingTypes();
            if (processingTypes.Contains(type))
            {
                return null;
            }

            try
            {
                processingTypes.Add(type);
                object result = null;
                var factory = RS.SimpleJsonUnity.SimpleJson.GetRegisteredAotFactory(type);
                if (factory != null)
                {
                    result = factory();
                }
                else
                {
                    result = Activator.CreateInstance(type);
                }
                if (result == null)
                    return null;
                var properties = GetOrBuildProperties(type);

                foreach (var prop in properties.Values.Where(p => p.CanDeserialize))
                {
                    if (dict.TryGetValue(prop.JsonFieldName, out var value))
                    {
                        var convertedValue = DeserializeObject(value, prop.Type);
                        prop.SetValue(result, convertedValue);
                    }
                }

                return result;
            }
            catch
            {
                return null;
            }
            finally
            {
                processingTypes.Remove(type);
            }
        }

        private object ConvertDictionaryKey(string key, Type keyType)
        {
            if (keyType == typeof(string))
                return key;
            if (keyType == typeof(int) && int.TryParse(key, out var intKey))
                return intKey;
            if (keyType == typeof(long) && long.TryParse(key, out var longKey))
                return longKey;
            return Convert.ChangeType(key, keyType, CultureInfo.InvariantCulture);
        }

        private object DeserializeFromArray(IList<object> array, Type type)
        {
            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                var arr = Array.CreateInstance(elementType, array.Count);
                for (int i = 0; i < array.Count; i++)
                {
                    arr.SetValue(DeserializeObject(array[i], elementType), i);
                }
                return arr;
            }

            if (!type.GetTypeInfo().IsGenericType)
                return array;

            var genericDef = type.GetGenericTypeDefinition();
            if (genericDef == typeof(List<>) || genericDef == typeof(IList<>) ||
                genericDef == typeof(IEnumerable<>) || genericDef == typeof(ICollection<>) ||
                genericDef == typeof(IReadOnlyList<>) || genericDef == typeof(IReadOnlyCollection<>))
            {
                var elementType = type.GetGenericArguments()[0];

                System.Collections.IList list = null;

                var factory = RS.SimpleJsonUnity.SimpleJson.GetRegisteredAotFactory(type);
                if (factory != null)
                {
                    list = factory() as System.Collections.IList;
                }

                if (list == null)
                {
                    var concreteListType = typeof(List<>).MakeGenericType(elementType);
                    factory = RS.SimpleJsonUnity.SimpleJson.GetRegisteredAotFactory(concreteListType);
                    if (factory != null)
                    {
                        list = factory() as System.Collections.IList;
                    }
                }

                if (list == null)
                {
                    try
                    {
                        var listType = typeof(List<>).MakeGenericType(elementType);
                        list = (System.Collections.IList)Activator.CreateInstance(listType);
                    }
                    catch
                    {
                        return array;
                    }
                }

                foreach (var item in array)
                {
                    var convertedItem = DeserializeObject(item, elementType);
                    list.Add(convertedItem);
                }

                return list;
            }

            return array;
        }

        private object ConvertPrimitive(object value, Type targetType)
        {
            if (value == null) return null;

            if (targetType == typeof(int))
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(long))
                return Convert.ToInt64(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(short))
                return Convert.ToInt16(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(byte))
                return Convert.ToByte(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(sbyte))
                return Convert.ToSByte(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(uint))
                return Convert.ToUInt32(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(ulong))
                return Convert.ToUInt64(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(ushort))
                return Convert.ToUInt16(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(float))
                return Convert.ToSingle(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(double))
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(decimal))
                return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(bool))
                return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(char))
                return Convert.ToChar(value, CultureInfo.InvariantCulture);

            try
            {
                return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            }
            catch
            {
                return value;
            }
        }

        private object DeserializeEnum(string value, Type type)
        {
            var cachedEnumsForType = _cachedEnums.GetOrAdd(type, t =>
            {
                var enumsForType = new ConcurrentDictionary<object, object>();
                var fields = type.GetRuntimeFields();
                foreach (var field in fields)
                {
                    if (field.Name == "value__")
                        continue;
                    var attribute = (ParameterAttribute)field.GetCustomAttribute(typeof(ParameterAttribute));
                    if (attribute != null)
                    {
                        enumsForType.GetOrAdd(attribute.Value, _ => field.GetValue(null));
                    }
                }
                return enumsForType;
            });

            return cachedEnumsForType.GetOrAdd(value, v =>
            {
                if (long.TryParse(v.ToString(), out var numVal))
                    return SafeEnumConversion(numVal, type);
                return Enum.Parse(type, v.ToString(), ignoreCase: true);
            });
        }

        private static Type GetPayloadType(string activityType)
        {
            switch (activityType)
            {
                case "CheckRunEvent":
                    return typeof(CheckRunEventPayload);
                case "CheckSuiteEvent":
                    return typeof(CheckSuiteEventPayload);
                case "CommitCommentEvent":
                    return typeof(CommitCommentPayload);
                case "CreateEvent":
                    return typeof(CreateEventPayload);
                case "DeleteEvent":
                    return typeof(DeleteEventPayload);
                case "ForkEvent":
                    return typeof(ForkEventPayload);
                case "IssueCommentEvent":
                    return typeof(IssueCommentPayload);
                case "IssuesEvent":
                    return typeof(IssueEventPayload);
                case "PullRequestEvent":
                    return typeof(PullRequestEventPayload);
                case "PullRequestReviewEvent":
                    return typeof(PullRequestReviewEventPayload);
                case "PullRequestReviewCommentEvent":
                    return typeof(PullRequestCommentPayload);
                case "PushEvent":
                    return typeof(PushEventPayload);
                case "ReleaseEvent":
                    return typeof(ReleaseEventPayload);
                case "StatusEvent":
                    return typeof(StatusEventPayload);
                case "WatchEvent":
                    return typeof(StarredEventPayload);
            }
            return typeof(ActivityPayload);
        }
    }
#endif
}
