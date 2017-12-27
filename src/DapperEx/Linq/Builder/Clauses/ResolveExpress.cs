﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Linq.Expressions;
using DapperEx.Linq.Helpers;

namespace DapperEx.Linq.Builder.Clauses
{
    public class ResolveExpress
    {
        public Dictionary<string,object> Argument;
        public string SqlWhere;
        public SqlParameter[] Paras;

        /// <summary>
        /// 解析lamdba，生成Sql查询条件
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public void ResolveExpression(Expression expression)
        {
            this.Argument = new Dictionary<string,object>();
            this.SqlWhere = Resolve(expression);
            this.Paras = Argument.Select(x => new SqlParameter(x.Key,x.Value)).ToArray();
        }

        private string Resolve(Expression expression)
        {
            if (expression is LambdaExpression)
            {
                LambdaExpression lambda = expression as LambdaExpression;
                expression = lambda.Body;
                return Resolve(expression);
            }
            if (expression is BinaryExpression)
            {
                BinaryExpression binary = expression as BinaryExpression;
                if (binary.Left is MemberExpression && binary.Right is ConstantExpression)//解析x=>x.Name=="123" x.Age==123这类
                    return ResolveFunc(binary.Left,binary.Right,binary.NodeType);
                if (binary.Left is MethodCallExpression && binary.Right is ConstantExpression)//解析x=>x.Name.Contains("xxx")==false这类的
                {
                    object value = (binary.Right as ConstantExpression).Value;
                    return ResolveLinqToObject(binary.Left,value,binary.NodeType);
                }
                if (binary.Left is MemberExpression && binary.Right is MemberExpression)//解析x=>x.Date==DateTime.Now这种
                {
                    LambdaExpression lambda = Expression.Lambda(binary.Right);
                    Delegate fn = lambda.Compile();
                    ConstantExpression value = Expression.Constant(fn.DynamicInvoke(null),binary.Right.Type);
                    return ResolveFunc(binary.Left,value,binary.NodeType);
                }
            }
            if (expression is UnaryExpression)
            {
                UnaryExpression unary = expression as UnaryExpression;
                if (unary.Operand is MethodCallExpression)//解析!x=>x.Name.Contains("xxx")或!array.Contains(x.Name)这类
                    return ResolveLinqToObject(unary.Operand,false);
                if (unary.Operand is MemberExpression && unary.NodeType == ExpressionType.Not)//解析x=>!x.isDeletion这样的 
                {
                    ConstantExpression constant = Expression.Constant(false);
                    return ResolveFunc(unary.Operand,constant,ExpressionType.Equal);
                }
            }
            if (expression is MemberExpression && expression.NodeType == ExpressionType.MemberAccess)//解析x=>x.isDeletion这样的 
            {
                MemberExpression member = expression as MemberExpression;
                ConstantExpression constant = Expression.Constant(true);
                return ResolveFunc(member,constant,ExpressionType.Equal);
            }
            if (expression is MethodCallExpression)//x=>x.Name.Contains("xxx")或array.Contains(x.Name)这类
            {
                MethodCallExpression methodcall = expression as MethodCallExpression;
                return ResolveLinqToObject(methodcall,true);
            }
            var body = expression as BinaryExpression;
            if (body == null)
                throw new Exception("无法解析" + expression);
            var Operator = Helper.GetOperator(body.NodeType);
            var left = Resolve(body.Left);
            var right = Resolve(body.Right);
            string result = string.Format("({0} {1} {2})",left,Operator,right);
            return result;
        }

        private string ResolveFunc(Expression left,Expression right,ExpressionType expressiontype)
        {
            var name = (left as MemberExpression).Member.Name;
            var value = (right as ConstantExpression).Value;
            var Operator = Helper.GetOperator(expressiontype);
            string compName = SetArgument(name,value.ToString());
            string result = string.Format("({0} {1} {2})",name,Operator,compName);
            return result;
        }

        private string ResolveLinqToObject(Expression expression,object value,ExpressionType? expressiontype = null)
        {
            var methodCall = expression as MethodCallExpression;
            var methodName = methodCall.Method.Name;
            switch (methodName)//这里其实还可以改成反射调用，不用写switch
            {
                case "Contains":
                    if (methodCall.Object != null)
                        return Like(methodCall);
                    return In(methodCall,value);
                case "Count":
                    return Len(methodCall,value,expressiontype.Value);
                case "LongCount":
                    return Len(methodCall,value,expressiontype.Value);
                default:
                    throw new Exception(string.Format("不支持{0}方法的查找！",methodName));
            }
        }

        private string SetArgument(string name,string value)
        {
            name = "@" + name;
            string temp = name;
            while (Argument.ContainsKey(temp))
            {
                int code = Guid.NewGuid().GetHashCode();
                if (code < 0)
                    code *= -1;
                temp = name + code;
            }
            Argument[temp] = value;
            return temp;
        }

        private string In(MethodCallExpression expression,object isTrue)
        {
            var argument1 = (expression.Arguments[0] as MemberExpression).Expression as ConstantExpression;
            var argument2 = expression.Arguments[1] as MemberExpression;
            var fieldArray = argument1.Value.GetType().GetFields().First();
            object[] array = fieldArray.GetValue(argument1.Value) as object[];
            List<string> SetInPara = new List<string>();
            for (int i = 0;i < array.Length;i++)
            {
                string namePara = "InParameter" + i;
                string value = array[i].ToString();
                string key = SetArgument(namePara,value);
                SetInPara.Add(key);
            }
            string name = argument2.Member.Name;
            string Operator = Convert.ToBoolean(isTrue) ? "in" : " not in";
            string compName = string.Join(",",SetInPara);
            string result = string.Format("{0} {1} ({2})",name,Operator,compName);
            return result;
        }

        private string Like(MethodCallExpression expression)
        {
            object tempVale = (expression.Arguments[0] as ConstantExpression).Value;
            string value = string.Format("%{0}%",tempVale);
            string name = (expression.Object as MemberExpression).Member.Name;
            string compName = SetArgument(name,value);
            string result = string.Format("{0} like {1}",name,compName);
            return result;
        }

        private string Len(MethodCallExpression expression,object value,ExpressionType expressiontype)
        {
            object name = (expression.Arguments[0] as MemberExpression).Member.Name;
            string Operator = Helper.GetOperator(expressiontype);
            string compName = SetArgument(name.ToString(),value.ToString());
            string result = string.Format("len({0}){1}{2}",name,Operator,compName);
            return result;
        }
    }
}
