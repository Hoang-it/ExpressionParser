using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;

public class Parser
{
    private static readonly HashSet<string> Operators = new HashSet<string> { "and", "or", "==", ">", "!", "<" };
    
    public static Node Parse(string expression)
    {
        const int MaxConditions = 10;
        const int MaxDepth = 5;
        Stack<Node> nodes = new Stack<Node>();
        Stack<string> operators = new Stack<string>();

        var expressionsFound = CountExpressions(expression);
        if (expressionsFound.Count > MaxConditions)
        {
            throw new InvalidOperationException($"Expression exceeds the maximum limit of {MaxConditions} conditions.");
        }

        Console.WriteLine("Expressions found:");
        foreach (var exp in expressionsFound)
        {
            Console.WriteLine(exp);
        }

        string[] tokens = expression.Replace("(", "( ").Replace(")", " )").Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var token in tokens)
        {
            if (token == "(" || token == ")")
            {
                // Parentheses don't count towards the condition limit.
                continue;
            }

            if (Operators.Contains(token))
            {
                while (operators.Count > 0 && Precedence(operators.Peek()) >= Precedence(token))
                {
                    nodes.Push(CreateNode(operators.Pop(), nodes, MaxDepth));
                }
                operators.Push(token);
            }
            else
            {
                if (token.StartsWith("!"))
                {
                    Node unaryNode = new Node("!");
                    unaryNode.Right = new Node(token.Substring(1));
                    CheckDepth(unaryNode, MaxDepth);
                    nodes.Push(unaryNode);
                }
                else
                {
                    nodes.Push(new Node(token));
                }
            }
        }

        while (operators.Count > 0)
        {
            nodes.Push(CreateNode(operators.Pop(), nodes, MaxDepth));
        }

        return nodes.Pop();
    }

    public static List<string> CountExpressions(string expression)
    {
        List<string> expressions = new List<string>();
        string[] tokens = expression.Replace("(", "( ").Replace(")", " )").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        bool inExpression = false;
        string currentExpression = "";

        foreach (var token in tokens)
        {
            if (token == "(" || token == ")")
            {
                // Skip parentheses.
                continue;
            }

            if (Operators.Contains(token))
            {
                if (inExpression)
                {
                    expressions.Add(currentExpression.Trim());
                    currentExpression = "";
                }
                inExpression = true;
            }

            currentExpression += token + " ";

            // If token is a standalone operator or boolean literal, add as an individual expression.
            if (token == "true" || token == "false")
            {
                expressions.Add(currentExpression.Trim());
                currentExpression = "";
            }
        }

        // Add the last expression if it wasn't added already.
        if (!string.IsNullOrEmpty(currentExpression.Trim()))
        {
            expressions.Add(currentExpression.Trim());
        }

        // Remove non-logical expressions like single identifiers.
        expressions.RemoveAll(exp => Operators.Contains(exp.Trim()));

        return expressions;
    }

    private static void CheckDepth(Node node, int maxDepth, int currentDepth = 0)
    {
        if (currentDepth > maxDepth)
        {
            throw new InvalidOperationException($"Expression exceeds the maximum depth of {maxDepth}.");
        }

        if (node.Left != null)
        {
            CheckDepth(node.Left, maxDepth, currentDepth + 1);
        }
        if (node.Right != null)
        {
            CheckDepth(node.Right, maxDepth, currentDepth + 1);
        }
    }

    private static Node CreateNode(string op, Stack<Node> nodes, int maxDepth)
    {
        Node right = nodes.Pop();
        Node left = nodes.Count > 0 ? nodes.Pop() : null;
        Node parent = new Node(op) { Left = left, Right = right };
        CheckDepth(parent, maxDepth);
        return parent;
    }

    private static int Precedence(string op)
    {
        switch (op)
        {
            case "or":
                return 1;
            case "and":
                return 2;
            case "==": case ">": case "<":
                return 3;
            case "!":
                return 4;
            default:
                return 0;
        }
    }

    public static void PrintTree(Node node, int indent = 0)
    {
        if (node == null) return;
        Console.WriteLine(new String(' ', indent) + node.Value);
        PrintTree(node.Left, indent + 2);
        PrintTree(node.Right, indent + 2);
    }

    public static string TreeToExpression(Node node)
    {
        if (node == null) return "";

        if (node.Left == null && node.Right == null)
        {
            return node.Value;
        }

        string leftExpr = TreeToExpression(node.Left);
        string rightExpr = TreeToExpression(node.Right);

        return $"({leftExpr} {node.Value} {rightExpr})";
    }

    public static Expression ToLambdaExpression(Node node, ParameterExpression param)
    {
        if (node == null)
            throw new ArgumentNullException(nameof(node));

        if (node.Left == null && node.Right == null)
        {
            var trimmedValue = node.Value.Trim('\'');
            if (DateTime.TryParseExact(trimmedValue, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateTimeValue))
            {
                return Expression.Constant(dateTimeValue); // Handle DateTime literals
            }
            else if (node.Value.StartsWith("'") && node.Value.EndsWith("'"))
            {
                return Expression.Constant(node.Value.Trim('\'')); // Handle string literals
            }
            else if (int.TryParse(node.Value, out int intValue))
            {
                return Expression.Constant(intValue); // Handle integers
            }
            else if (node.Value == "true" || node.Value == "false")
            {
                return Expression.Constant(bool.Parse(node.Value)); // Handle booleans
            }
            else
            {
                return Expression.PropertyOrField(param, node.Value); // Handle variables
            }
        }

        var leftExpr = node.Left != null ? ToLambdaExpression(node.Left, param) : null;
        var rightExpr = ToLambdaExpression(node.Right, param);

        switch (node.Value)
        {
            case "and":
                return Expression.AndAlso(leftExpr, rightExpr);
            case "or":
                return Expression.OrElse(leftExpr, rightExpr);
            case "==":
                return Expression.Equal(leftExpr, rightExpr);
            case ">":
                return Expression.GreaterThan(leftExpr, rightExpr);
            case "<":
                return Expression.LessThan(leftExpr, rightExpr);
            case "!":
                return Expression.Not(rightExpr);
            default:
                throw new NotSupportedException($"Operator {node.Value} is not supported.");
        }
    }
}

public class Program
{
    public static void Main()
    {
        try
        {
            string expression = "( d > '2023-01-01' and (( b > 3 or !c ) ) and (c or a == 'hoang'))";
            var expressionsFound = Parser.CountExpressions(expression);
            Console.WriteLine($"Expression count: {expressionsFound.Count}");
            Console.WriteLine("Expressions found:");
            foreach (var exp in expressionsFound)
            {
                Console.WriteLine(exp);
            }

            Node root = Parser.Parse(expression);
            Parser.PrintTree(root);

            var param = Expression.Parameter(typeof(DynamicClass), "x");
            var lambdaExpression = Parser.ToLambdaExpression(root, param);

            var lambda = Expression.Lambda<Func<DynamicClass, bool>>(lambdaExpression, param);
            Console.WriteLine("Lambda Expression: " + lambda);
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}

public class DynamicClass
{
    public string a { get; set; }
    public int b { get; set; }
    public bool c { get; set; }
    public DateTime d { get; set; }
}
