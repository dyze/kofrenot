using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Views;

/// <summary>
/// Places a graph in ranks, as a family tree: a link whose role is <see cref="GraphRole.Down"/> (or
/// <see cref="GraphRole.Up"/>) puts its lower node at least one rank below the other; nodes linked
/// <see cref="GraphRole.Beside"/> form a group, side by side on one rank. A cycle is cut where it
/// closes. Each rank is ordered to cross fewer links (barycentres), then each group pulled over the
/// groups it hangs from.
/// </summary>
internal static class LayeredLayout
{
    private const int OrderSweeps = 8;
    private const int PlaceSweeps = 4;
    private const double Step = GraphLayout.CardWidth + GraphLayout.ColumnGap;

    public static Dictionary<Guid, NodePosition> Place(GraphContent content)
    {
        var groups = GroupsOf(content);
        var (parents, children) = RankLinksOf(content, groups);
        var ranks = RanksOf(groups, parents, children, FreeNeighboursOf(content, groups));
        var rows = OrderedRows(groups, ranks, parents, children);
        var lefts = Lefts(groups, rows, parents, children);

        var positions = new Dictionary<Guid, NodePosition>();
        for (var group = 0; group < groups.Count; group++)
        {
            for (var index = 0; index < groups[group].Count; index++)
            {
                positions[groups[group][index]] = new NodePosition(
                    groups[group][index],
                    lefts[group] + (index * Step) + (GraphLayout.CardWidth / 2),
                    (ranks[group] * (GraphLayout.CardHeight + GraphLayout.RankGap)) + (GraphLayout.CardHeight / 2));
            }
        }

        return positions;
    }

    // The nodes linked beside one another, each group in the order its nodes were reached; the groups in the order of their first node.
    private static List<List<Guid>> GroupsOf(GraphContent content)
    {
        var leader = content.Nodes.ToDictionary(node => node.Id, node => node.Id);
        Guid Find(Guid node) => leader[node] == node ? node : leader[node] = Find(leader[node]);

        foreach (var edge in content.Edges.Where(edge => edge.Role == GraphRole.Beside))
        {
            var (from, to) = (Find(edge.Link.From), Find(edge.Link.To));
            if (from != to)
            {
                leader[to] = from;
            }
        }

        return content.Nodes.GroupBy(node => Find(node.Id)).Select(group => group.Select(node => node.Id).ToList()).ToList();
    }

    // Between groups: the groups each one hangs from, and those hanging from it, a cycle cut where it closes.
    private static (List<HashSet<int>> Parents, List<HashSet<int>> Children) RankLinksOf(GraphContent content, List<List<Guid>> groups)
    {
        var groupOf = GroupIndex(groups);
        var parents = groups.Select(_ => new HashSet<int>()).ToList();
        var children = groups.Select(_ => new HashSet<int>()).ToList();
        foreach (var edge in content.Edges.Where(edge => edge.Role is GraphRole.Down or GraphRole.Up))
        {
            var (upper, lower) = edge.Role == GraphRole.Down ? (edge.Link.From, edge.Link.To) : (edge.Link.To, edge.Link.From);
            var (parent, child) = (groupOf[upper], groupOf[lower]);
            if (parent != child && !Reaches(children, child, parent))
            {
                parents[child].Add(parent);
                children[parent].Add(child);
            }
        }

        return (parents, children);
    }

    private static Dictionary<Guid, int> GroupIndex(List<List<Guid>> groups) =>
        groups.SelectMany((group, index) => group.Select(node => (node, index))).ToDictionary(pair => pair.node, pair => pair.index);

    // Whether 'to' is below 'from' already: a link from 'to' down to 'from' would close a cycle.
    private static bool Reaches(List<HashSet<int>> children, int from, int to)
    {
        var seen = new HashSet<int> { from };
        var next = new Stack<int>([from]);
        while (next.TryPop(out var group))
        {
            if (group == to)
            {
                return true;
            }

            foreach (var child in children[group].Where(seen.Add))
            {
                next.Push(child);
            }
        }

        return false;
    }

    private static List<HashSet<int>> FreeNeighboursOf(GraphContent content, List<List<Guid>> groups)
    {
        var groupOf = GroupIndex(groups);
        var neighbours = groups.Select(_ => new HashSet<int>()).ToList();
        foreach (var edge in content.Edges.Where(edge => edge.Role == GraphRole.Free))
        {
            var (from, to) = (groupOf[edge.Link.From], groupOf[edge.Link.To]);
            neighbours[from].Add(to);
            neighbours[to].Add(from);
        }

        return neighbours;
    }

    // Each group one rank below its lowest parent; a group with no parent just above its highest child;
    // a group with neither on its first free neighbour's rank. The top rank is 0.
    private static int[] RanksOf(List<List<Guid>> groups, List<HashSet<int>> parents, List<HashSet<int>> children, List<HashSet<int>> free)
    {
        var order = TopologicalOrder(groups.Count, parents, children);
        var ranks = new int[groups.Count];
        foreach (var group in order)
        {
            ranks[group] = parents[group].Count == 0 ? 0 : parents[group].Max(parent => ranks[parent]) + 1;
        }

        foreach (var group in order.AsEnumerable().Reverse().Where(group => parents[group].Count == 0 && children[group].Count > 0))
        {
            ranks[group] = children[group].Min(child => ranks[child]) - 1;
        }

        var isRanked = Enumerable.Range(0, groups.Count).Select(group => parents[group].Count > 0 || children[group].Count > 0).ToArray();
        foreach (var group in Enumerable.Range(0, groups.Count).Where(group => !isRanked[group]))
        {
            if (free[group].FirstOrDefault(neighbour => isRanked[neighbour], -1) is var neighbour and >= 0)
            {
                ranks[group] = ranks[neighbour];
            }
        }

        var top = ranks.Length == 0 ? 0 : ranks.Min();
        return ranks.Select(rank => rank - top).ToArray();
    }

    // Parents before children, otherwise in the groups' order (Kahn).
    private static List<int> TopologicalOrder(int count, List<HashSet<int>> parents, List<HashSet<int>> children)
    {
        var waiting = parents.Select(groupParents => groupParents.Count).ToArray();
        var ready = new PriorityQueue<int, int>(Enumerable.Range(0, count).Where(group => waiting[group] == 0).Select(group => (group, group)));
        var order = new List<int>(count);
        while (ready.TryDequeue(out var group, out _))
        {
            order.Add(group);
            foreach (var child in children[group].Where(child => --waiting[child] == 0))
            {
                ready.Enqueue(child, child);
            }
        }

        return order;
    }

    // The groups of each rank, left to right: sorted by the mean place of their parents (going down)
    // or children (going up), a group with none keeping its place.
    private static List<List<int>> OrderedRows(List<List<Guid>> groups, int[] ranks, List<HashSet<int>> parents, List<HashSet<int>> children)
    {
        var rows = Enumerable.Range(0, groups.Count).GroupBy(group => ranks[group]).OrderBy(row => row.Key).Select(row => row.ToList()).ToList();
        var place = new double[groups.Count];
        void Number(List<int> row)
        {
            for (var index = 0; index < row.Count; index++)
            {
                place[row[index]] = index;
            }
        }

        rows.ForEach(Number);
        for (var sweep = 0; sweep < OrderSweeps; sweep++)
        {
            var down = sweep % 2 == 0;
            var neighbours = down ? parents : children;
            foreach (var row in down ? rows : rows.AsEnumerable().Reverse())
            {
                var sorted = row.OrderBy(group => neighbours[group].Count == 0 ? place[group] : neighbours[group].Average(other => place[other])).ThenBy(group => place[group]).ToList();
                row.Clear();
                row.AddRange(sorted);
                Number(row);
            }
        }

        return rows;
    }

    // Each group's left edge: side by side in its row's order, then pulled over the groups it hangs
    // from (going down) or that hang from it (going up), each row moved as a whole to stay balanced.
    private static double[] Lefts(List<List<Guid>> groups, List<List<int>> rows, List<HashSet<int>> parents, List<HashSet<int>> children)
    {
        var lefts = new double[groups.Count];
        double Width(int group) => (groups[group].Count * Step) - GraphLayout.ColumnGap;
        double Centre(int group) => lefts[group] + (Width(group) / 2);

        foreach (var row in rows)
        {
            var left = 0.0;
            foreach (var group in row)
            {
                lefts[group] = left;
                left += Width(group) + GraphLayout.ColumnGap;
            }
        }

        for (var sweep = 0; sweep < PlaceSweeps; sweep++)
        {
            var down = sweep % 2 == 0;
            var neighbours = down ? parents : children;
            foreach (var row in down ? rows.Skip(1) : rows.AsEnumerable().Reverse().Skip(1))
            {
                var wanted = row.ToDictionary(group => group, group => neighbours[group].Count == 0 ? Centre(group) : neighbours[group].Average(Centre));
                var previousRight = double.NegativeInfinity;
                foreach (var group in row)
                {
                    lefts[group] = Math.Max(wanted[group] - (Width(group) / 2), previousRight + GraphLayout.ColumnGap);
                    previousRight = lefts[group] + Width(group);
                }

                var shift = row.Average(group => wanted[group] - Centre(group));
                row.ForEach(group => lefts[group] += shift);
            }
        }

        return lefts;
    }
}
