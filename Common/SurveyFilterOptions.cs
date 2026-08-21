using MainProject.Application.DTO;

namespace MainProject.Application.Support;

public static class SurveyFilterOptions
{
    public static IReadOnlyList<SelectionOption> Build(IEnumerable<SelectionOption> options)
    {
        return options
            .Where(option => option.Id > 0 && !string.IsNullOrWhiteSpace(option.Name))
            .Select(option => new
            {
                option.Id,
                Name = option.Name.Trim()
            })
            .GroupBy(option => option.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var orderedItems = group.OrderBy(option => option.Id).ToArray();
                var ids = orderedItems
                    .Select(option => option.Id)
                    .Distinct()
                    .ToArray();

                return new SelectionOption
                {
                    Id = ids[0],
                    Name = orderedItems[0].Name,
                    Ids = ids
                };
            })
            .OrderBy(option => option.Name, AppListPaging.RuStringComparer)
            .ThenBy(option => option.Id)
            .ToArray();
    }

    public static IReadOnlyList<int> ExpandSelectedIds(
        IReadOnlyCollection<int> selectedIds,
        IReadOnlyCollection<SelectionOption> options)
    {
        var expandedIds = selectedIds
            .Where(id => id > 0)
            .ToHashSet();

        foreach (var option in options)
        {
            var optionIds = option.Ids.Count > 0 ? option.Ids : [option.Id];
            if (optionIds.Any(expandedIds.Contains))
            {
                expandedIds.UnionWith(optionIds.Where(id => id > 0));
            }
        }

        return expandedIds.OrderBy(id => id).ToArray();
    }
}
