﻿using System.Collections.Generic;

namespace Battlegrounds.Lua.Parsing {
    
    /// <summary>
    /// Abstract representation of a Lua Expression
    /// </summary>
    public abstract record LuaExpr;

    /// <summary>
    /// Abstract extension representation of a lua expression intended for looking up data
    /// </summary>
    public abstract record LuaLookupIdExpr : LuaExpr;

    /// <summary>
    /// 
    /// </summary>
    public abstract record LuaStatement : LuaExpr;

    /// <summary>
    /// 
    /// </summary>
    public abstract record LuaValueExpr : LuaExpr;

    /// <summary>
    /// Represents a comment in Lua code
    /// </summary>
    public record LuaComment(string Comment) : LuaExpr;

    /// <summary>
    /// Represents a lua scope
    /// </summary>
    public record LuaScope(List<LuaExpr> ScopeBody) : LuaExpr;

    /// <summary>
    /// Representation of an Operator expression (Should never be executed)
    /// </summary>
    /// <param name="Type">The operator type of the operator expression.</param>
    public record LuaOpExpr(object Type) : LuaExpr;

    /// <summary>
    /// Representation of a keyword (Should never be executed)
    /// </summary>
    public record LuaKeyword(string Keyword) : LuaExpr;

    /// <summary>
    /// Binary Lua expression with an operator defined.
    /// </summary>
    public record LuaBinaryExpr(LuaExpr Left, LuaExpr Right, string Operator) : LuaExpr;

    /// <summary>
    /// 
    /// </summary>
    public record LuaAssignExpr(LuaExpr Left, LuaExpr Right, bool Local) : LuaBinaryExpr(Left, Right, "=");

    /// <summary>
    /// 
    /// </summary>
    public record LuaTupleExpr(List<LuaExpr> Values) : LuaExpr;

    /// <summary>
    /// Value expression.
    /// </summary>
    public record LuaConstValueExpr(LuaValue Value) : LuaValueExpr;

    /// <summary>
    /// Negate expression
    /// </summary>
    public record LuaNegateExpr(LuaExpr Expr) : LuaExpr;

    /// <summary>
    /// Identifier expression.
    /// </summary>
    public record LuaIdentifierExpr(string Identifier) : LuaLookupIdExpr;

    /// <summary>
    /// Indexing expression.
    /// </summary>
    public record LuaIndexExpr(LuaExpr Key) : LuaLookupIdExpr;

    /// <summary>
    /// Overall table expression.
    /// </summary>
    public record LuaTableExpr(List<LuaExpr> SubExpressions) : LuaExpr;

    /// <summary>
    /// Value lookup expression
    /// </summary>
    public record LuaLookupExpr(LuaExpr Left, LuaLookupIdExpr Right) : LuaExpr;

    /// <summary>
    /// 
    /// </summary>
    public record LuaArguments(List<LuaExpr> Arguments) : LuaExpr;

    /// <summary>
    /// 
    /// </summary>
    public record LuaEmptyParenthesisGroup() : LuaArguments(new List<LuaExpr>());

    /// <summary>
    /// 
    /// </summary>
    public record LuaSingleElementParenthesisGroup(List<LuaExpr> Exprs) : LuaArguments(Exprs);

    /// <summary>
    /// 
    /// </summary>
    public record LuaCallExpr(LuaExpr ToCall, LuaArguments Arguments) : LuaExpr;

    /// <summary>
    /// 
    /// </summary>
    public record LuaFuncExpr(LuaArguments Arguments, LuaScope Body) : LuaValueExpr;

    /// <summary>
    /// 
    /// </summary>
    public record LuaReturnStatement(LuaExpr Value) : LuaStatement;

}
