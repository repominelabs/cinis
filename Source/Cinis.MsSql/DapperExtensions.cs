﻿using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Data;
using System.Data.SqlClient;
using Dapper;

namespace Cinis.MsSql;

public static partial class DapperExtensions
{
    private static string GetTableName<T>()
        => typeof(T).GetCustomAttribute<TableAttribute>().Name;

    private static string GetTableSchema<T>()
        => typeof(T).GetCustomAttribute<TableAttribute>().Schema;

    private static IEnumerable<PropertyInfo> GetProperties<T>()
        => typeof(T).GetProperties();

    private static PropertyInfo? GetPrimaryKey<T>()
        => typeof(T).GetProperties().Where(x => x.GetCustomAttributes().Any(y => y.GetType() == typeof(KeyAttribute))).FirstOrDefault();

    private static IEnumerable<string?> GetColumns<T>()
        => typeof(T).GetProperties().Where(e => e.Name != GetPrimaryKey<T>()?.Name && e.GetCustomAttribute<ColumnAttribute>() != null).Select(e => e.GetCustomAttribute<ColumnAttribute>()?.Name);

    private static IEnumerable<string> GetColumnPropertyNames<T>()
        => typeof(T).GetProperties().Where(e => e.Name != GetPrimaryKey<T>()?.Name && e.GetCustomAttribute<ColumnAttribute>() != null).Select(e => e.Name);

    public static dynamic Create<T>(this SqlConnection connection, T entity, SqlTransaction? transaction = null)
    {
        if (connection is null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        var stringOfColumns = string.Join(", ", GetColumns<T>());
        var stringOfParameters = string.Join(", ", GetColumnPropertyNames<T>().Select(e => "@" + e));
        var sql = $"insert into {GetTableSchema<T>()}.{GetTableName<T>()} ({stringOfColumns}) values ({stringOfParameters}) returning {GetPrimaryKey<T>()?.Name}";

        var result = connection.Execute(sql, entity, transaction);
        return result;
    }

    public static List<T> Read<T>(this SqlConnection connection, string? whereClause = null, SqlTransaction? transaction = null)
    {
        if (connection is null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        string sql;
        if (!string.IsNullOrEmpty(whereClause))
        {
            sql = $"select * from {GetTableSchema<T>()}.{GetTableName<T>()} where {whereClause}";
        }
        else
        {
            sql = $"select * from {GetTableSchema<T>()}.{GetTableName<T>()}";
        }

        List<T> result = connection.Query<T>(sql, null, transaction).ToList();
        return result;
    }

    public static dynamic Update<T>(this SqlConnection connection, T entity, bool nullable = false, string? whereClause = null, SqlTransaction? transaction = null)
    {
        if (connection is null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        string stringOfSets;
        if (nullable)
        {
            stringOfSets = string.Join(", ", GetProperties<T>().Where(e => e.GetCustomAttribute<ColumnAttribute>() != null).Select(e => $"{e.GetCustomAttribute<ColumnAttribute>().Name} = @{e.Name}"));
        }
        else
        {
            string[] propertyNames = entity.GetType().GetProperties().Where(x => x.GetCustomAttribute<ColumnAttribute>() != null && x.GetValue(entity) != null).Select(x => x.GetCustomAttribute<ColumnAttribute>().Name).ToArray();
            stringOfSets = string.Join(" , ", propertyNames.Select(propertyName => propertyName + " = @" + entity.GetType().GetProperties().Where(x => x.GetCustomAttribute<ColumnAttribute>() != null && x.GetCustomAttribute<ColumnAttribute>().Name == propertyName).Select(e => e.Name).FirstOrDefault()));
        }

        string sql;
        if (!string.IsNullOrEmpty(whereClause))
        {
            sql = $"update {GetTableSchema<T>()}.{GetTableName<T>()} set {stringOfSets} where {whereClause}";
        }
        else
        {
            sql = $"update {GetTableSchema<T>()}.{GetTableName<T>()} set {stringOfSets} where {GetPrimaryKey<T>()?.GetCustomAttribute<ColumnAttribute>()?.Name} = @{GetPrimaryKey<T>()?.Name}";
        }

        var result = connection.Execute(sql, entity, transaction);
        return result;
    }

    public static dynamic Delete<T>(this SqlConnection connection, string? whereClause = null, SqlTransaction? transaction = null)
    {
        if (connection is null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        string sql;
        if (string.IsNullOrEmpty(whereClause))
        {
            sql = $"delete from {GetTableSchema<T>()}.{GetTableName<T>()}";
        }
        else
        {
            sql = $"delete from {GetTableSchema<T>()}.{GetTableName<T>()} where {whereClause}";
        }

        var result = connection.Execute(sql, null, transaction);
        return result;
    }
}
