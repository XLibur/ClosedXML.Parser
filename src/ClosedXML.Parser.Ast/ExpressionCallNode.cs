namespace XLibur.Parser;

/// <summary>
/// A call of the value of an expression, e.g. <c>LAMBDA(x,x*2)(3)</c>. The first child is the
/// expression that gives the value to call, the rest are the arguments.
/// </summary>
public record ExpressionCallNode : AstNode
{
    public override string GetDisplayString(ReferenceStyle style) => "()";
}
