﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace DevExtreme.AspNet.Data {

    class DevExtremeGroup {
        public object key;
        public IList<object> items;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? count;
    }


    class GroupHelper<T> : ExpressionCompiler {
        IDictionary<string, Func<T, object>> _accessors = new Dictionary<string, Func<T, object>>();
        IEnumerable<T> _data;

        public GroupHelper(IEnumerable<T> data) {
            _data = data;
        }

        public IList<DevExtremeGroup> Group(IEnumerable<GroupingInfo> groupInfo) {
            return Group(_data, groupInfo);
        }

        IList<DevExtremeGroup> Group(IEnumerable<T> data, IEnumerable<GroupingInfo> groupInfo) {
            var isMostNested = groupInfo.Count() < 2;
            var groups = Group(data, groupInfo.First(), isMostNested);

            if(!isMostNested) {
                groups = groups
                    .Select(g => new DevExtremeGroup {
                        key = g.key,
                        items = Group(g.items.Cast<T>(), groupInfo.Skip(1))
                            .Cast<object>()
                            .ToArray()
                    })
                    .ToArray();
            }

            return groups;
        }


        IList<DevExtremeGroup> Group(IEnumerable<T> data, GroupingInfo groupInfo, bool isMostNested) {
            var map = new Dictionary<object, DevExtremeGroup>();
            var groups = new List<DevExtremeGroup>();
            var expanded = groupInfo.IsExpanded == null || groupInfo.IsExpanded.Value == true;
            var countOnly = isMostNested && !expanded;

            foreach(var item in data) {
                var key = GetKey(item, groupInfo);
                if(!map.ContainsKey(key)) {
                    var newGroup = new DevExtremeGroup { key = key };
                    map[key] = newGroup;
                    groups.Add(newGroup);
                }

                var group = map[key];

                if(countOnly) {
                    if(!group.count.HasValue)
                        group.count = 0;
                    group.count++;
                } else {
                    if(group.items == null)
                        group.items = new List<object>();
                    group.items.Add(item);
                }
            }

            return groups;
        }

        object GetKey(T obj, GroupingInfo groupInfo) {
            var memberValue = GetMember(obj, groupInfo.Selector);

            var intervalString = groupInfo.GroupInterval;
            if(String.IsNullOrEmpty(intervalString))
                return memberValue;

            if(Char.IsDigit(intervalString[0])) {
                var number = Convert.ToDecimal(memberValue);
                var interval = Decimal.Parse(intervalString);
                return number - number % interval;
            }

            switch(intervalString) {
                case "year":
                    return Convert.ToDateTime(memberValue).Year;
                case "quarter":
                    return (int)Math.Ceiling(Convert.ToDateTime(memberValue).Month / 3.0);
                case "month":
                    return Convert.ToDateTime(memberValue).Month ;
                case "day":
                    return Convert.ToDateTime(memberValue).Day;
                case "dayOfWeek":
                    return (int)Convert.ToDateTime(memberValue).DayOfWeek;
                case "hour":
                    return Convert.ToDateTime(memberValue).Hour;
                case "minute":
                    return Convert.ToDateTime(memberValue).Minute;
                case "second":
                    return Convert.ToDateTime(memberValue).Second;
            }

            throw new NotSupportedException();
        }


        object GetMember(T obj, string name) {
            if(!_accessors.ContainsKey(name)) {
                var param = CreateItemParam(typeof(T));

                _accessors[name] = Expression.Lambda<Func<T, object>>(
                    Expression.Convert(CompileAccessorExpression(param, name), typeof(Object)),
                    param
                ).Compile();
            }

            return _accessors[name](obj);
        }


    }

}
