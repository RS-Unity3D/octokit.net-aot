#if USE_AOT_JSON


namespace RS.OctokitAOT
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Reflection;
    using Octokit;
    using Octokit.Internal;
    using Octokit.Reflection;
    using RS.SimpleJsonUnity;

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

        public override void ClearCache()
        {
            base.ClearCache();
            _cachedEnums.Clear();
            _propertiesCache.Clear();
        }

        #region 序列化

        protected override bool TrySerializeKnownTypes(object input, out object output)
        {
            if (input == null) { output = null; return false; }

            if (input is Enum e)
            {
                output = e.ToParameter();
                return true;
            }

            return base.TrySerializeKnownTypes(input, out output);
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

            if (TrySerializeKnownTypes(input, out output))
                return true;

            try
            {
                if (input is IDictionary dict)
                {
                    var result = new Dictionary<string, object>();
                    foreach (DictionaryEntry entry in dict)
                    {
                        var key = entry.Key?.ToString() ?? "";
                        result[key] = WrapSerializeValue(entry.Value);
                    }
                    output = result;
                    return true;
                }

                if (input is IEnumerable enumerable && !(input is string))
                {
                    var list = new List<object>();
                    foreach (var item in enumerable)
                    {
                        list.Add(WrapSerializeValue(item));
                    }
                    output = list;
                    return true;
                }

                var properties = GetOrBuildProperties(type);
                var obj = new Dictionary<string, object>();

                foreach (var prop in properties.Values)
                {
                    if (!prop.CanSerialize) continue;
                    var value = prop.GetValue(input);
                    if (value == null) continue;
                    obj[prop.JsonFieldName] = WrapSerializeValue(value);
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

        private object WrapSerializeValue(object value)
        {
            if (value == null) return null;
            if (value.GetType().GetTypeInfo().IsPrimitive || value is string || value is decimal)
                return value;
            if (value is Enum enumVal)
                return enumVal.ToParameter();
            if (TrySerializeNonPrimitiveObject(value, out var nested))
                return nested;
            return value;
        }

        #endregion

        #region 反序列化

        public override object DeserializeObject(object value, Type type)
        {
            if (value == null) return null;
            if (type == null) return value;
            if (type == typeof(object)) return value;

            var typeInfo = type.GetTypeInfo();

            if (typeInfo.IsAssignableFrom(value.GetType().GetTypeInfo()))
                return value;

            Type underlyingType = Nullable.GetUnderlyingType(type);
            if (underlyingType != null)
            {
                type = underlyingType;
                typeInfo = type.GetTypeInfo();
            }

            if (Octokit.Reflection.ReflectionUtils.IsStringEnumWrapper(type))
            {
                return DeserializeStringEnum(value, type);
            }

            if (typeInfo.IsEnum)
            {
                return DeserializeEnumValue(value, type);
            }

            if (value is IDictionary nonGenericDict && !(value is IDictionary<string, object>))
            {
                var genericDict = new Dictionary<string, object>();
                foreach (DictionaryEntry entry in nonGenericDict)
                {
                    genericDict[entry.Key?.ToString() ?? ""] = entry.Value;
                }
                return DeserializeObject(genericDict, type);
            }

            if (value is IEnumerable enumerable && !(value is string) && !(value is IDictionary) && !(value is IDictionary<string, object>) && !(value is IList<object>))
            {
                var genericList = new List<object>();
                foreach (var item in enumerable)
                {
                    genericList.Add(item);
                }
                return DeserializeObject(genericList, type);
            }

            return base.DeserializeObject(value, type);
        }

        protected override bool DeserializeFromJsonObject(
            object value, Type type, out object output)
        {
            var dict = value as IDictionary<string, object>;
            if (dict == null) { output = null; return false; }

            if (type == typeof(Activity) && dict.TryGetValue("type", out var typeObj))
            {
                _activityType = typeObj?.ToString();
            }

            if (type == typeof(ActivityPayload) && !string.IsNullOrEmpty(_activityType))
            {
                type = GetPayloadType(_activityType);
            }

            if (Octokit.Reflection.ReflectionUtils.IsTypeDictionary(type))
            {
                return base.DeserializeFromJsonObject(value, type, out output);
            }

            var processingTypes = GetProcessingTypes();
            if (processingTypes.Contains(type))
            {
                output = null;
                return true;
            }

            try
            {
                processingTypes.Add(type);
                object result = null;
                var factory = SimpleJson.GetRegisteredAotFactory(type);
                if (factory != null)
                {
                    result = factory();
                }
                else
                {
                    result = Activator.CreateInstance(type);
                }
                if (result == null)
                {
                    output = null;
                    return false;
                }
                var properties = GetOrBuildProperties(type);

                foreach (var kvp in dict)
                {
                    PropertyOrField prop;
                    if (!properties.TryGetValue(kvp.Key, out prop))
                        continue;

                    if (!prop.CanWrite || prop.IsStatic)
                        continue;

                    object convertedValue;
                    try
                    {
                        convertedValue = DeserializeObject(kvp.Value, prop.Type);
                    }
                    catch
                    {
                        continue;
                    }

                    var pi = prop.MemberInfo as PropertyInfo;
                    if (pi != null)
                    {
                        var setter = pi.GetSetMethod(true);
                        if (setter != null)
                        {
                            try { setter.Invoke(result, new object[] { convertedValue }); }
                            catch { }
                        }
                    }
                    else
                    {
                        var fi = prop.MemberInfo as FieldInfo;
                        if (fi != null && !fi.IsInitOnly)
                        {
                            try { fi.SetValue(result, convertedValue); }
                            catch { }
                        }
                    }
                }

                output = result;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeserializeFromJsonObject failed for {type.Name}: {ex.Message}");
                System.Console.Error.WriteLine($"[AOT] DeserializeFromJsonObject failed for {type.Name}: {ex.GetType().Name}: {ex.Message}");
                output = null;
                return false;
            }
            finally
            {
                processingTypes.Remove(type);
            }
        }

        protected override object DeserializeArray(
            IList<object> jsonArray, Type type,
            IJsonSerializerStrategy strategy)
        {
            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                var arr = Array.CreateInstance(elementType, jsonArray.Count);
                for (int i = 0; i < jsonArray.Count; i++)
                {
                    arr.SetValue(DeserializeObject(jsonArray[i], elementType), i);
                }
                return arr;
            }

            if (!type.GetTypeInfo().IsGenericType)
                return jsonArray;

            var genericDef = type.GetGenericTypeDefinition();
            if (genericDef == typeof(List<>) || genericDef == typeof(IList<>) ||
                genericDef == typeof(IEnumerable<>) || genericDef == typeof(ICollection<>) ||
                genericDef == typeof(IReadOnlyList<>) || genericDef == typeof(IReadOnlyCollection<>))
            {
                var elementType = type.GetGenericArguments()[0];

                System.Collections.IList list = null;

                var factory = SimpleJson.GetRegisteredAotFactory(type);
                if (factory != null)
                {
                    list = factory() as System.Collections.IList;
                }

                if (list == null)
                {
                    var concreteListType = typeof(List<>).MakeGenericType(elementType);
                    factory = SimpleJson.GetRegisteredAotFactory(concreteListType);
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
                        return jsonArray;
                    }
                }

                foreach (var item in jsonArray)
                {
                    var convertedItem = DeserializeObject(item, elementType);
                    list.Add(convertedItem);
                }

                return list;
            }

            return jsonArray;
        }

        #endregion

        #region 枚举反序列化

        private object DeserializeStringEnum(object value, Type type)
        {
            var stringValue = value as string;
            if (stringValue != null)
            {
                try
                {
                    var ctor = type.GetConstructor(new Type[] { typeof(string) });
                    if (ctor != null)
                    {
                        return ctor.Invoke(new object[] { stringValue });
                    }
                    return Activator.CreateInstance(type, stringValue);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"DeserializeStringEnum failed: {ex.GetType().Name}: {ex.Message}");
                    if (ex is TargetInvocationException tie && tie.InnerException != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"  Inner: {tie.InnerException.GetType().Name}: {tie.InnerException.Message}");
                    }
                }
            }

            return null;
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
                return RS.SimpleJsonUnity.ReflectionUtils.SafeEnumConversionFromNumber(value, type);
            }

            return DeserializeEnum(value?.ToString() ?? "", type);
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
                    return RS.SimpleJsonUnity.ReflectionUtils.SafeEnumConversionFromNumber(numVal, type);
                return Enum.Parse(type, v.ToString(), ignoreCase: true);
            });
        }

        #endregion

        #region 属性缓存

        private IDictionary<string, PropertyOrField> GetOrBuildProperties(Type type)
        {
            return _propertiesCache.GetOrAdd(type, t =>
            {
                var allProperties = new List<PropertyInfo>();
                var allFields = new List<FieldInfo>();
                var currentType = t;
                while (currentType != null && currentType != typeof(object))
                {
                    allProperties.AddRange(currentType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly));
                    allFields.AddRange(currentType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly));
                    currentType = currentType.GetTypeInfo().BaseType;
                }

                var seen = new HashSet<string>();
                var seenJsonKey = new Dictionary<string, PropertyOrField>();

                foreach (var p in allProperties)
                {
                    if (p.GetIndexParameters().Length > 0) continue;
                    if (!p.CanRead) continue;
                    if (seen.Contains(p.Name)) continue;
                    seen.Add(p.Name);
                    var pf = new PropertyOrField(p);
                    if (!seenJsonKey.ContainsKey(pf.JsonFieldName))
                        seenJsonKey[pf.JsonFieldName] = pf;
                }

                foreach (var f in allFields)
                {
                    if (seen.Contains(f.Name)) continue;
                    if (f.IsInitOnly) continue;
                    seen.Add(f.Name);
                    var pf = new PropertyOrField(f);
                    if (!seenJsonKey.ContainsKey(pf.JsonFieldName))
                        seenJsonKey[pf.JsonFieldName] = pf;
                }

                return seenJsonKey;
            });
        }

        #endregion

        #region Activity Payload 类型解析

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

        #endregion
    }

}
#endif
