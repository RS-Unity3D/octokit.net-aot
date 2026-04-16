using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
    /// <summary>
    /// AOT 桥接适配文件
    /// 实现 RS.SimpleJson-Unity 的 IJsonSerializerStrategy 接口
    /// 复用 Octokit 现有的序列化逻辑
    /// </summary>
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

            try
            {
                var properties = GetOrBuildProperties(type);
                var sb = new System.Text.StringBuilder();
                sb.Append('{');
                bool first = true;

                foreach (var prop in properties.Values.Where(p => p.CanSerialize))
                {
                    var value = prop.GetValue(input);
                    if (value != null)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        
                        sb.Append('"');
                        sb.Append(EscapeString(prop.JsonFieldName));
                        sb.Append("\":");
                        SerializeValue(value, sb);
                    }
                }

                sb.Append('}');
                output = sb.ToString();
                return true;
            }
            catch
            {
                output = null;
                return false;
            }
        }

        private void SerializeValue(object value, System.Text.StringBuilder sb)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }

            var type = value.GetType();

            if (value is string str)
            {
                sb.Append('"');
                sb.Append(EscapeString(str));
                sb.Append('"');
            }
            else if (type.GetTypeInfo().IsPrimitive || value is decimal)
            {
                sb.Append(value.ToString().ToLower());
            }
            else if (value is DateTime dt)
            {
                sb.Append('"');
                sb.Append(dt.ToString("o"));
                sb.Append('"');
            }
            else if (value is DateTimeOffset dto)
            {
                sb.Append('"');
                sb.Append(dto.ToString("o"));
                sb.Append('"');
            }
            else if (value is Enum e)
            {
                sb.Append('"');
                sb.Append(e.ToParameter());
                sb.Append('"');
            }
            else if (value is System.Collections.IDictionary dict)
            {
                sb.Append('{');
                bool first = true;
                foreach (DictionaryEntry entry in dict)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append('"');
                    sb.Append(EscapeString(entry.Key?.ToString() ?? ""));
                    sb.Append("\":");
                    SerializeValue(entry.Value, sb);
                }
                sb.Append('}');
            }
            else if (value is System.Collections.IEnumerable enumerable)
            {
                sb.Append('[');
                bool first = true;
                foreach (var item in enumerable)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    SerializeValue(item, sb);
                }
                sb.Append(']');
            }
            else
            {
                if (TrySerializeNonPrimitiveObject(value, out var serialized))
                {
                    sb.Append(serialized);
                }
                else
                {
                    sb.Append("null");
                }
            }
        }

        private static string EscapeString(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            
            var sb = new System.Text.StringBuilder();
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        public override object DeserializeObject(object value, Type type)
        {
            if (value == null)
                return null;

            if (type == null)
                return value;

            var typeInfo = type.GetTypeInfo();
            
            if (typeInfo.IsPrimitive || type == typeof(string) || type == typeof(decimal))
            {
                return ConvertValue(value, type);
            }

            var stringValue = value as string;
            var dictValue = value as IDictionary<string, object>;
            var arrayValue = value as IList<object>;

            if (stringValue != null)
            {
                return DeserializeFromString(stringValue, type);
            }

            if (dictValue != null)
            {
                return DeserializeFromDictionary(dictValue, type);
            }

            if (arrayValue != null)
            {
                return DeserializeFromArray(arrayValue, type);
            }

            if (value is IDictionary)
            {
                var genericDict = new Dictionary<string, object>();
                foreach (DictionaryEntry entry in (IDictionary)value)
                {
                    genericDict[entry.Key?.ToString() ?? ""] = entry.Value;
                }
                return DeserializeFromDictionary(genericDict, type);
            }

            if (value is System.Collections.IEnumerable enumerable && !(value is string))
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

        private object DeserializeFromString(string value, Type type)
        {
            if (ReflectionUtils.IsNullableType(type))
            {
                type = Nullable.GetUnderlyingType(type);
            }

            var typeInfo = type.GetTypeInfo();

            if (typeInfo.IsEnum)
            {
                return DeserializeEnum(value, type);
            }

            if (ReflectionUtils.IsTypeGenericeCollectionInterface(type))
            {
                var innerType = ReflectionUtils.GetGenericListElementType(type);
                if (innerType.IsAssignableFrom(typeof(string)))
                {
                    return value.Split(',');
                }
            }

            if (ReflectionUtils.IsStringEnumWrapper(type))
            {
                try
                {
                    var factory = RS.SimpleJsonUnity.SimpleJson.GetRegisteredAotFactory(type);
                    if (factory != null)
                    {
                        var instance = factory();
                        var valueProp = type.GetProperty("Value");
                        if (valueProp != null)
                            valueProp.SetValue(instance, value, null);
                        return instance;
                    }
                    return Activator.CreateInstance(type, value);
                }
                catch
                {
                    return null;
                }
            }

            return value;
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
                try
                {
                    var factory = RS.SimpleJsonUnity.SimpleJson.GetRegisteredAotFactory(type);
                    if (factory != null)
                    {
                        var instance = factory();
                        var valueProp = type.GetProperty("Value");
                        if (valueProp != null)
                            valueProp.SetValue(instance, "null", null);
                        return instance;
                    }
                    return Activator.CreateInstance(type, "null");
                }
                catch
                {
                    return null;
                }
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
                        var convertedValue = ConvertValue(value, prop.Type);
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

        private object DeserializeFromArray(IList<object> array, Type type)
        {
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

        private object ConvertValue(object value, Type targetType)
        {
            if (value == null)
                return null;

            if (targetType == null || targetType == typeof(object))
                return value;

            if (targetType.GetTypeInfo().IsAssignableFrom(value.GetType().GetTypeInfo()))
                return value;

            if (value is IDictionary<string, object> dict)
            {
                return DeserializeFromDictionary(dict, targetType);
            }

            if (value is IList<object> array)
            {
                return DeserializeFromArray(array, targetType);
            }

            if (value is string strValue)
            {
                if (targetType == typeof(DateTime) || targetType == typeof(DateTime?))
                {
                    if (DateTime.TryParse(strValue, out var dt))
                        return dt;
                }
                else if (targetType == typeof(DateTimeOffset) || targetType == typeof(DateTimeOffset?))
                {
                    if (DateTimeOffset.TryParse(strValue, out var dto))
                        return dto;
                }
                return DeserializeFromString(strValue, targetType);
            }

            if (value is long longValue)
            {
                if (targetType == typeof(DateTime) || targetType == typeof(DateTime?))
                {
                    return DateTimeOffset.FromUnixTimeSeconds(longValue).DateTime;
                }
                else if (targetType == typeof(DateTimeOffset) || targetType == typeof(DateTimeOffset?))
                {
                    return DateTimeOffset.FromUnixTimeSeconds(longValue);
                }
                else if (targetType == typeof(int) || targetType == typeof(int?))
                {
                    return (int)longValue;
                }
            }

            if (value is double doubleValue)
            {
                if (targetType == typeof(DateTime) || targetType == typeof(DateTime?))
                {
                    return DateTimeOffset.FromUnixTimeSeconds((long)doubleValue).DateTime;
                }
                else if (targetType == typeof(DateTimeOffset) || targetType == typeof(DateTimeOffset?))
                {
                    return DateTimeOffset.FromUnixTimeSeconds((long)doubleValue);
                }
                else if (targetType == typeof(int) || targetType == typeof(int?))
                {
                    return (int)doubleValue;
                }
                else if (targetType == typeof(long) || targetType == typeof(long?))
                {
                    return (long)doubleValue;
                }
            }

            try
            {
                return Convert.ChangeType(value, targetType);
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

            return cachedEnumsForType.GetOrAdd(value, v => Enum.Parse(type, v.ToString(), ignoreCase: true));
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
