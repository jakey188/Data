﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using Dapper;
using Dapper.Linq.Builder.Clauses;
using DapperEx.Linq.Builder;
using DapperEx.Linq.Builder.Clauses;
using DapperEx.Linq.Types;

namespace DapperEx.Linq
{
    internal class Query<T> : IQuery<T>
    {
        private readonly DapperDbContext _db;
        private readonly SqlBuilder<T> _builder;

        public Query(DapperDbContext db, Expression<Func<T, bool>> expression = null)
        {
            _db = db;
            _builder = new SqlBuilder<T>(db.Adapter);
            this.Where(expression);
        }

        public Query(SqlBuilder<T> builder, DapperDbContext db, Expression<Func<T, bool>> expression = null)
        {
            _db = db;
            _builder = builder;
            this.Where(expression);
        }

        /// <summary>
        /// 条件筛选
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public IQuery<T> Where(Expression<Func<T, bool>> expression)
        {
            var vistor = new WhereExpressionVisitor<T>();
            if (expression != null)
            {
                vistor.Evaluate(expression,_builder);
            }
            return this;
        }

        /// <summary>
        /// 条件筛选
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public IQuery<T> Where(string predicate)
        {
            var vistor = new WhereExpressionVisitor<T>();
            vistor.Evaluate(null, _builder, predicate);
            return this;
        }

        /// <summary>
        /// 将源数据对象转换到新对象中
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <returns></returns>
        public IQuery<TResult> Select<TResult>(Expression<Func<T,TResult>> selector)
        {
            new SelectClause<T>(_builder).Build(selector.Body);

            var builder = new SqlBuilder<TResult>(_db.Adapter)
            {
                Table = _builder.Table,
                Parameters = _builder.Parameters,
                Take = _builder.Take,
                Where = _builder.Where,
                Order = _builder.Order,
                GroupBy = _builder.GroupBy,
                SelectField = _builder.SelectField
            };
            return new Query<TResult>(builder,_db);
        }

        /// <summary>
        ///  将IQuery转换为List《T》集合
        /// </summary>
        /// <returns></returns>
        public List<T> ToList()
        {
            string sql = _builder.GetQueryString();
            return _db.Connection.Query<T>(sql,_builder.Parameters,_db.Transaction).AsList<T>();
        }

        /// <summary>
        ///  将IQuery转换为List<T>分页集合
        /// </summary>
        /// <returns></returns>
        public List<T> ToPageList(int pageIndex,int pageSize,out int total)
        {
            total = 0;
            if (pageIndex < 1)
                pageIndex = 1;

            var data = _db.Connection.Query<T>(_builder.GetQueryPageString(pageIndex,pageSize),_builder.Parameters,_db.Transaction).AsList<T>();

            if (data != null && data.Count > 0 && pageSize > 0)
            {
                total = _db.Connection.ExecuteScalar<int>($"SELECT COUNT(*) FROM {_builder.Table} {_builder.Where}",
                   _builder.Parameters);
            }

            return data;
        }


        /// <summary>
        /// 获取序列总记录数
        /// </summary>
        /// <returns></returns>
        public int Count()
        {
            _builder.SelectField = new List<string>() { "Count(*)" };

            return _db.Connection.ExecuteScalar<int>(_builder.GetQueryString(), _builder.Parameters, _db.Transaction);
        }

        /// <summary>
        /// 从起始点向后取指定条数的数据
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="count"></param>
        /// <returns></returns>
        public IQuery<T> Take(int count)
        {
            _builder.Take = count;
            return this;
        }

        /// <summary>
        /// 按组
        /// </summary>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public IGrouping<T> GroupBy<TKey>(Expression<Func<T,TKey>> predicate)
        {
            new GroupByClause<T>(_builder).Build(predicate.Body);
            return new Grouping<T>(_builder, _db);
        }

        /// <summary>
        ///  返回序列中的第一个元素。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T First()
        {
            _builder.Take = 1;
            var reval = this.ToList();
            return reval.First();
        }

        /// <summary>
        ///返回序列中的第一个元素,如果序列为NULL返回default(T)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T FirstOrDefault()
        {
            _builder.Take = 1;
            var reval = this.ToList();
            if (reval == null || reval.Count == 0)
            {
                return default(T);
            }
            return reval.First();
        }

        /// <summary>
        ///  确定序列是否包含任何元素
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public bool Any()
        {
            return this.Count() > 0;
        }

        /// <summary>
        /// 获取最大值
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="selector"></param>
        /// <returns></returns>
        public TResult Max<TResult>(Expression<Func<T,TResult>> selector)
        {
            new SelectClause<T>(_builder).Build(selector.Body,AverageFunction.MAX);
            return _db.Connection.ExecuteScalar<TResult>(_builder.GetQueryString(),_builder.Parameters,_db.Transaction);
        }

        /// <summary>
        /// 获取最小值
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="selector"></param>
        /// <returns></returns>
        public TResult Min<TResult>(Expression<Func<T,TResult>> selector)
        {
            new SelectClause<T>(_builder).Build(selector.Body,AverageFunction.MIN);
            return _db.Connection.ExecuteScalar<TResult>(_builder.GetQueryString(),_builder.Parameters,_db.Transaction);
        }

        /// <summary>
        /// 获取总和
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="selector"></param>
        /// <returns></returns>
        public TResult Sum<TResult>(Expression<Func<T,TResult>> selector)
        {
            new SelectClause<T>(_builder).Build(selector.Body,AverageFunction.SUM);
            return _db.Connection.ExecuteScalar<TResult>(_builder.GetQueryString(),_builder.Parameters,_db.Transaction);
        }

        /// <summary>
        /// 根据键按升序对序列的元素排序
        /// </summary>
        /// <param name="predicate">键名</param>
        /// <returns></returns>
        public IQuery<T> OrderBy(Expression<Func<T,object>> predicate)
        {
            new OrderByClause<T>(_builder).Build(predicate.Body);
            return this;
        }

        /// <summary>
        /// 根据键按升序对序列的元素排序
        /// </summary>
        /// <param name="predicate">键名</param>
        /// <returns></returns>
        public IQuery<T> ThenBy(Expression<Func<T,object>> predicate)
        {
            new OrderByClause<T>(_builder).Build(predicate.Body);
            return this;
        }

        /// <summary>
        /// 根据键按降序对序列的元素排序
        /// </summary>
        /// <param name="predicate">键名</param>
        /// <returns></returns>
        public IQuery<T> ThenByDescending(Expression<Func<T,object>> predicate)
        {
            new OrderByClause<T>(_builder).Build(predicate.Body,true);
            return this;
        }

        /// <summary>
        /// 根据键按降序对序列的元素排序
        /// </summary>
        /// <param name="predicate">键名</param>
        /// <returns></returns>
        public IQuery<T> OrderByDescending(Expression<Func<T,object>> predicate)
        {
            new OrderByClause<T>(_builder).Build(predicate.Body, true);
            return this;
        }

        //public IQuery<TResult> Join<TResult>(Expression<Func<T,TResult,bool>> expression)
        //{
        //    new JoinClause<T>(_builder).Build(expression,JoinType.InnerJoin);

        //    var builder = new SqlBuilder<TResult>
        //    {
        //        Adapter = _builder.Adapter,
        //        Table = _builder.Table,
        //        Parameters = _builder.Parameters,
        //        Take = _builder.Take,
        //        Where = _builder.Where,
        //        Order = _builder.Order,
        //        GroupBy = _builder.GroupBy,
        //        SelectField = _builder.SelectField
        //    };

        //    return new Query<TResult>(builder,this._db.Connection,this._db.Transaction);
        //}

        //public IQuery<TResult> LeftJoin<TResult>(Expression<Func<T,TResult,bool>> expression)
        //{
        //    new JoinClause<T>(_builder).Build(expression,JoinType.LeftJoin);

        //    var builder = new SqlBuilder<TResult>
        //    {
        //        Adapter = _builder.Adapter,
        //        Table = _builder.Table,
        //        Parameters = _builder.Parameters,
        //        Take = _builder.Take,
        //        Where = _builder.Where,
        //        Order = _builder.Order,
        //        GroupBy = _builder.GroupBy,
        //        SelectField = _builder.SelectField
        //    };

        //    return new Query<TResult>(builder,this._db.Connection,this._db.Transaction);
        //}

        //public IQuery<TResult> RightJoin<TResult>(Expression<Func<T,TResult,bool>> expression)
        //{
        //    new JoinClause<T>(_builder).Build(expression,JoinType.RightJoin);

        //    var builder = new SqlBuilder<TResult>
        //    {
        //        Adapter = _builder.Adapter,
        //        Table = _builder.Table,
        //        Parameters = _builder.Parameters,
        //        Take = _builder.Take,
        //        Where = _builder.Where,
        //        Order = _builder.Order,
        //        GroupBy = _builder.GroupBy,
        //        SelectField = _builder.SelectField
        //    };

        //    return new Query<TResult>(builder,this._db.Connection,this._db.Transaction);
        //}

        public override string ToString()
        {
            return _builder.GetQueryString();
        }
    }
}
