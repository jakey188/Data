﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Dapper.Linq.Helpers
{
    

    internal class CacheHelper
    {
        private static readonly ConcurrentDictionary<Type,TableInfo> TypeTableInfo = new ConcurrentDictionary<Type,TableInfo>();

        internal static int Size
        {
            get { return TypeTableInfo.Count; }
        }

        public static TableInfo GetTableInfo(Expression expression)
        {
            var exp = Helper.GetMemberExpression(expression);
            if (exp == null)
                return null;

            return GetTableInfo(exp.Expression.Type);
        }

        public static TableInfo GetTableInfo(Type type)
        {
            TableInfo tableInfo;
            if (!TypeTableInfo.TryGetValue(type,out tableInfo))
            {
                var properties = new Dictionary<string,string>();
                type.GetProperties().ToList().ForEach(
                        x =>
                        {
                            var col = (ColumnAttribute)x.GetCustomAttribute(typeof(ColumnAttribute));
                            properties.Add(x.Name,(col != null) ? col.Name : x.Name);
                        }
                    );

                var attrib = (TableAttribute)type.GetCustomAttribute(typeof(TableAttribute));

                tableInfo = new TableInfo
                {
                    Name = (attrib != null ? attrib.Name : type.Name),
                    Columns = properties,
                    Alias = string.Format("t{0}",Size + 1)
                };
                TypeTableInfo.TryAdd(type,tableInfo);
            }
            return tableInfo;
        }

        public static string GetTableAlias(Expression expression)
        {
            return GetTableInfo(expression).Alias;
        }
    }
}
