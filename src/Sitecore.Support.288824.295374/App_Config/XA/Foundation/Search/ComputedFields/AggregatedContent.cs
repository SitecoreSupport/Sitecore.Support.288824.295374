namespace Sitecore.Support.XA.Foundation.Search.ComputedFields
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Xml;
  using Microsoft.Extensions.DependencyInjection;
  using Sitecore.ContentSearch;
  using Sitecore.ContentSearch.ComputedFields;
  using Sitecore.Data;
  using Sitecore.Data.Comparers;
  using Sitecore.Data.Items;
  using Sitecore.StringExtensions;
  using Sitecore.XA.Foundation.LocalDatasources.Services;
  using Sitecore.XA.Foundation.Multisite;
  using Sitecore.XA.Foundation.Multisite.Extensions;
  using Sitecore.XA.Foundation.Search.ComputedFields;
  using Sitecore.XA.Foundation.SitecoreExtensions.Extensions;

  public class AggregatedContent : Sitecore.XA.Foundation.Search.ComputedFields.AggregatedContent
  {

    private readonly MediaItemContentExtractor _mediaContentExtractor;
    readonly ID SnippetItemTemplateId = new ID("{2B35E65C-137A-4641-B683-D223CA7CFAC0}"); 

    public AggregatedContent()
    {
      _mediaContentExtractor = new MediaItemContentExtractor();
    }
    public AggregatedContent(XmlNode configurationNode)
    {
      _mediaContentExtractor = new MediaItemContentExtractor(configurationNode);
    }

    public override object ComputeFieldValue(IIndexable indexable)
    {
      Item item = indexable as SitecoreIndexableItem;
      if (item == null)
      {
        return null;
      }

      if (item.Paths.IsMediaItem)
      {
        var computeFieldValue = _mediaContentExtractor.ComputeFieldValue(indexable) ?? String.Empty;
        computeFieldValue += FormattableString.Invariant($" {item.Name}");
        if (!item.DisplayName.IsNullOrEmpty() && item.Name != item.DisplayName)
        {
          computeFieldValue += FormattableString.Invariant($" {item.DisplayName}");
        }
        return computeFieldValue;
      }

      #region fix issue# 288824
      /*
      if (!item.IsPageItem() && !IsPoi.Verify(item))
      {
        return null;
      }
      */
      #endregion

      ISet<Item> dataFolders = new HashSet<Item>();
      foreach (Item folder in new[] { Sitecore.DependencyInjection.ServiceLocator.ServiceProvider.GetService<IMultisiteContext>().GetDataItem(item), Sitecore.DependencyInjection.ServiceLocator.ServiceProvider.GetService<ILocalDatasourceService>().GetPageDataItem(item) })
      {
        if (folder != null)
        {
          dataFolders.Add(folder);
        }
      }

      List<Item> items = new List<Item> { item };
      items.AddRange(GetFieldReferences(item, dataFolders));
      items.AddRange(GetLayoutReferences(item, dataFolders));

      int k = 0;
      while (k < items.Count)
      {
        for (; k < items.Count; k++)
        {
          if (ChildrenGroupingTemplateIds.Any(templateId => items[k].Template.DoesTemplateInheritFrom(templateId)))
          {
            var unique = GetUnique(items[k].Children, items);
            items.AddRange(unique);
          }
          else if ((CompositeTemplateIds.Any(templateId => items[k].Template.DoesTemplateInheritFrom(templateId))) || items[k].DoesItemInheritFrom(SnippetItemTemplateId))
          {
            var unique = GetUnique(GetLayoutReferences(items[k], dataFolders), items);
            items.AddRange(unique);
          }
        }
      }

      ProviderIndexConfiguration config = ContentSearchManager.GetIndex(indexable).Configuration;
      return items.Distinct(new ItemIdComparer()).SelectMany(i => ExtractTextFields(i, config));
    }
  }

}