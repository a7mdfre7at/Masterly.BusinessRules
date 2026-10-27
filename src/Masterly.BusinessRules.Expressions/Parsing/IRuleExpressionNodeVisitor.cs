namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// Double-dispatch over the closed set of node types: the interpreter, the text renderers and the typed compiler
    /// are all visitors, so adding a node type is a compile error everywhere it must be handled.
    /// </summary>
    internal interface IRuleExpressionNodeVisitor<out TResult>
    {
        TResult VisitLiteral(LiteralNode node);

        TResult VisitIdentifier(IdentifierNode node);

        TResult VisitParameter(ParameterNode node);

        TResult VisitMember(MemberNode node);

        TResult VisitMethodCall(MethodCallNode node);

        TResult VisitUnary(UnaryNode node);

        TResult VisitBinary(BinaryNode node);

        TResult VisitIn(InNode node);
    }
}
