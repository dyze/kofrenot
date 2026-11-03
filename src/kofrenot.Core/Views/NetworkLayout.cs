using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Views;

/// <summary>
/// Places a graph as a network (Fruchterman–Reingold): linked nodes pull one another, all nodes push
/// one another apart, each move shorter than the last. The nodes start on a spiral from the root, in
/// the order they were reached, so the result is the same each time; the nodes moved by hand stay
/// where they are, and pull or push the others.
/// </summary>
internal static class NetworkLayout
{
    private const int Rounds = 150;

    // The distance two linked nodes settle at.
    private const double Ideal = GraphLayout.CardWidth + (2 * GraphLayout.ColumnGap);

    // Golden angle: the spiral's points never line up.
    private const double Turn = 2.399963229728653;

    public static Dictionary<Guid, NodePosition> Place(GraphContent content, IReadOnlyDictionary<Guid, NodePosition> fixedPositions)
    {
        var nodes = content.Nodes.Select(node => node.Id).ToList();
        var index = nodes.Select((node, position) => (node, position)).ToDictionary(pair => pair.node, pair => pair.position);
        var (x, y) = (new double[nodes.Count], new double[nodes.Count]);
        var isFixed = nodes.Select(fixedPositions.ContainsKey).ToArray();
        for (var node = 0; node < nodes.Count; node++)
        {
            (x[node], y[node]) = fixedPositions.TryGetValue(nodes[node], out var position)
                ? (position.X, position.Y)
                : (Ideal * Math.Sqrt(node) * Math.Cos(node * Turn), Ideal * Math.Sqrt(node) * Math.Sin(node * Turn));
        }

        var links = content.Edges.Select(edge => (From: index[edge.Link.From], To: index[edge.Link.To])).ToList();
        for (var round = 0; round < Rounds; round++)
        {
            var heat = Ideal * (1 - ((double)round / Rounds));
            Move(x, y, isFixed, links, heat);
        }

        return nodes.ToDictionary(node => node, node => new NodePosition(node, Math.Round(x[index[node]]), Math.Round(y[index[node]])));
    }

    private static void Move(double[] x, double[] y, bool[] isFixed, List<(int From, int To)> links, double heat)
    {
        var (dx, dy) = (new double[x.Length], new double[x.Length]);
        for (var first = 0; first < x.Length; first++)
        {
            for (var second = first + 1; second < x.Length; second++)
            {
                var (offsetX, offsetY, distance) = Offset(x, y, first, second);
                var push = Ideal * Ideal / distance;
                Add(dx, dy, first, second, offsetX / distance * push, offsetY / distance * push);
            }
        }

        foreach (var (from, to) in links.Where(link => link.From != link.To))
        {
            var (offsetX, offsetY, distance) = Offset(x, y, from, to);
            var pull = distance * distance / Ideal;
            Add(dx, dy, from, to, -offsetX / distance * pull, -offsetY / distance * pull);
        }

        for (var node = 0; node < x.Length; node++)
        {
            var length = Math.Sqrt((dx[node] * dx[node]) + (dy[node] * dy[node]));
            if (!isFixed[node] && length > 0)
            {
                var step = Math.Min(length, heat);
                x[node] += dx[node] / length * step;
                y[node] += dy[node] / length * step;
            }
        }
    }

    // From the second node to the first; two nodes on one point are set apart along x, the first to the right.
    private static (double X, double Y, double Distance) Offset(double[] x, double[] y, int first, int second)
    {
        var (offsetX, offsetY) = (x[first] - x[second], y[first] - y[second]);
        var distance = Math.Sqrt((offsetX * offsetX) + (offsetY * offsetY));
        return distance > 0.01 ? (offsetX, offsetY, distance) : (1, 0, 1);
    }

    // A force on the first node, and its opposite on the second.
    private static void Add(double[] dx, double[] dy, int first, int second, double forceX, double forceY)
    {
        dx[first] += forceX;
        dy[first] += forceY;
        dx[second] -= forceX;
        dy[second] -= forceY;
    }
}
