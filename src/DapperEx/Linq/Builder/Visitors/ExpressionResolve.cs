﻿using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Dapper;
using DapperEx.Linq.Helpers;
using DapperEx.Linq.Types;

namespace DapperEx.Linq.Builder.Visitors
{
    internal class ExpressionResolve
    {
        public List<MemberNode> VisitMember(Expression expression)
        {
            var list = new List<MemberNode>();
            switch (expression.NodeType)
            {
                case ExpressionType.Convert:
                case ExpressionType.MemberAccess:
                    var name = GetMemberInfo(Helper.GetMemberExpression(expression));
                    list.Add(name);
                    break;
                case ExpressionType.New:
                    var newExpressions = (NewExpression)expression;
                    var nameArr = newExpressions.Members.Select(x => x.Name).ToArray();
                    int i = 0;
                    foreach (MemberExpression memberExp in newExpressions.Arguments)
                    {
                        var node = GetMemberInfo(memberExp);
                        if (!node.FieldName.Equals(nameArr[i]))
                            node.SelectFiledAliasName = nameArr[i];
                        list.Add(node);
                        i++;
                    }
                    break;
                case ExpressionType.MemberInit:
                    var memberInitExpression = (MemberInitExpression)expression;
                    foreach (var item in memberInitExpression.Bindings)
                    {
                        var memberExpression = ((MemberAssignment)item).Expression;
                        var node = GetMemberInfo(Helper.GetMemberExpression(memberExpression));
                        var model = new MemberNode
                        {
                            FieldName = node.FieldName,
                            SelectFiledAliasName = item.Member.Name == node.FieldName ? "" : item.Member.Name,
                            TableName = node.TableName,
                            TableAliasName = node.TableAliasName
                        };
                        list.Add(model);
                    }
                    break;
            }
            return list;
        }

        private MemberNode GetMemberInfo(MemberExpression memberExpression)
        {
            var member = CacheHelper.GetTableInfo(memberExpression);
            return new MemberNode()
            {
                TableName = member.Name,
                TableAliasName = member.Alias,
                FieldName = Helper.GetPropertyNameFromExpression(memberExpression)
            };
        }
    }
}
