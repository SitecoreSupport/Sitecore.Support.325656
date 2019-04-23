namespace Sitecore.Support.Framework.Publishing.DataPromotion
{
  using Sitecore.Framework.Conditions;
  using Sitecore.Framework.Publishing.DataPromotion;
  using Sitecore.Framework.Publishing.Item;
  using Sitecore.Framework.Publishing.Locators;
  using Sitecore.Framework.Publishing.Manifest;
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Threading.Tasks;
  public class ItemManifestPromoteWorker : IItemPromoteWorker
  {
    protected readonly FieldReportSpecification _fieldsToReport = new FieldReportSpecification();
    protected readonly IManifestRepository _manifestRepo;
    protected readonly IItemWriteRepository _itemRepo;
    protected readonly Guid _manifestId;
    protected readonly bool _calculateResults;

    public ItemManifestPromoteWorker(
      IManifestRepository manifestRepo,
      IItemWriteRepository itemRepo,
      Guid manifestId,
      bool calculateResults,
      FieldReportSpecification fieldsToReport)
    {
      Condition.Requires<IManifestRepository>(manifestRepo, nameof(manifestRepo)).IsNotNull<IManifestRepository>();
      Condition.Requires<IItemWriteRepository>(itemRepo, nameof(itemRepo)).IsNotNull<IItemWriteRepository>();
      Condition.Requires<FieldReportSpecification>(fieldsToReport, nameof(fieldsToReport)).IsNotNull<FieldReportSpecification>();
      this._manifestRepo = manifestRepo;
      this._itemRepo = itemRepo;
      this._fieldsToReport = fieldsToReport;
      this._manifestId = manifestId;
      this._calculateResults = calculateResults;
    }

    public virtual async Task DeleteItems(IReadOnlyCollection<Guid> itemIds)
    {
      IEnumerable<ItemChange> source = await this._itemRepo.DeleteItems(itemIds, this._calculateResults).ConfigureAwait(false);
      if (!this._calculateResults)
        return;
      await this._manifestRepo.AddManifestResults<ItemResult>(this._manifestId, source.OfType<ItemDeleted>().Select<ItemDeleted, ManifestOperationResult<ItemResult>>((Func<ItemDeleted, ManifestOperationResult<ItemResult>>)(d => new ManifestOperationResult<ItemResult>(d.Id, ManifestOperationResultType.Deleted, ManifestEntityType.Item, ItemResult.Deleted(new Sitecore.Framework.Publishing.Manifest.ItemProperties(d.Properties.Data.Name, d.Properties.Data.TemplateId, d.Properties.Data.ParentId, d.Properties.Data.MasterId))))).ToArray<ManifestOperationResult<ItemResult>>()).ConfigureAwait(false);
    }

    public virtual async Task DeleteVariants(IReadOnlyCollection<IItemVariantLocator> uris)
    {
      IEnumerable<ItemChange> source = await this._itemRepo.DeleteVariants((IReadOnlyCollection<IItemVariantIdentifier>)uris, this._calculateResults).ConfigureAwait(false);
      if (!this._calculateResults)
        return;
      await this._manifestRepo.AddManifestResults<ItemResult>(this._manifestId, source.OfType<ItemExists>().Where<ItemExists>((Func<ItemExists, bool>)(change => change.ChangeType == DataChangeType.Updated)).Select<ItemExists, ManifestOperationResult<ItemResult>>((Func<ItemExists, ManifestOperationResult<ItemResult>>)(change => new ManifestOperationResult<ItemResult>(change.Id, ManifestOperationResultType.Modified, ManifestEntityType.Item, ItemResult.Modified(new Sitecore.Framework.Publishing.Manifest.ItemProperties(change.Properties.Data.Name, change.Properties.Data.TemplateId, change.Properties.Data.ParentId, change.Properties.Data.MasterId), (Sitecore.Framework.Publishing.Manifest.ItemProperties)null, (IDictionary<IVarianceIdentifier, ResultChangeType>)change.Variances.ToDictionary<KeyValuePair<IVarianceIdentifier, DataChangeType>, IVarianceIdentifier, ResultChangeType>((Func<KeyValuePair<IVarianceIdentifier, DataChangeType>, IVarianceIdentifier>)(v => v.Key), (Func<KeyValuePair<IVarianceIdentifier, DataChangeType>, ResultChangeType>)(v => ItemManifestPromoteWorker.MapChangeType(v.Value))), Enumerable.Empty<FieldResult>())))).Where<ManifestOperationResult<ItemResult>>((Func<ManifestOperationResult<ItemResult>, bool>)(r => r != null)).ToArray<ManifestOperationResult<ItemResult>>()).ConfigureAwait(false);
    }

    public virtual async Task SaveVariants(IReadOnlyCollection<IItemVariant> data)
    {
      IEnumerable<ItemChange> source = await this._itemRepo.SaveVariants(data, this._calculateResults, this._fieldsToReport).ConfigureAwait(false);
      if (!this._calculateResults)
        return;
      await this._manifestRepo.AddManifestResults<ItemResult>(this._manifestId, source.Where<ItemChange>((Func<ItemChange, bool>)(change => change.ChangeType != DataChangeType.Unchanged)).Select<ItemChange, ManifestOperationResult<ItemResult>>((Func<ItemChange, ManifestOperationResult<ItemResult>>)(change =>
      {
        switch (change.ChangeType)
        {
          case DataChangeType.Created:
            return new ManifestOperationResult<ItemResult>(change.Id, ManifestOperationResultType.Created, ManifestEntityType.Item, ItemResult.Created(new Sitecore.Framework.Publishing.Manifest.ItemProperties(change.Properties.Data.Name, change.Properties.Data.TemplateId, change.Properties.Data.ParentId, change.Properties.Data.MasterId), (IEnumerable<IVarianceIdentifier>)change.Variances.Select<KeyValuePair<IVarianceIdentifier, DataChangeType>, IVarianceIdentifier>((Func<KeyValuePair<IVarianceIdentifier, DataChangeType>, IVarianceIdentifier>)(v => v.Key)).ToArray<IVarianceIdentifier>()));
          case DataChangeType.Updated:
            ItemPropertiesUpdated properties = change.Properties as ItemPropertiesUpdated;
            Sitecore.Framework.Publishing.Manifest.ItemProperties previous = properties == null ? (Sitecore.Framework.Publishing.Manifest.ItemProperties)null : new Sitecore.Framework.Publishing.Manifest.ItemProperties(properties.Original.Name, properties.Original.TemplateId, properties.Original.ParentId, properties.Original.MasterId);
            return new ManifestOperationResult<ItemResult>(change.Id, ManifestOperationResultType.Modified, ManifestEntityType.Item, ItemResult.Modified(new Sitecore.Framework.Publishing.Manifest.ItemProperties(change.Properties.Data.Name, change.Properties.Data.TemplateId, change.Properties.Data.ParentId, change.Properties.Data.MasterId), previous, (IDictionary<IVarianceIdentifier, ResultChangeType>)change.Variances.Where(v => v.Value != DataChangeType.Unchanged).ToDictionary<KeyValuePair<IVarianceIdentifier, DataChangeType>, IVarianceIdentifier, ResultChangeType>((Func<KeyValuePair<IVarianceIdentifier, DataChangeType>, IVarianceIdentifier>)(v => v.Key), (Func<KeyValuePair<IVarianceIdentifier, DataChangeType>, ResultChangeType>)(v => ItemManifestPromoteWorker.MapChangeType(v.Value))), (IEnumerable<FieldResult>)change.InvariantFields.Concat<FieldDataChange>(change.LanguageVariantFields.Values.SelectMany<IEnumerable<FieldDataChange>, FieldDataChange>((Func<IEnumerable<FieldDataChange>, IEnumerable<FieldDataChange>>)(lvf => lvf))).Concat<FieldDataChange>(change.VariantFields.SelectMany<KeyValuePair<IVarianceIdentifier, IEnumerable<FieldDataChange>>, FieldDataChange>((Func<KeyValuePair<IVarianceIdentifier, IEnumerable<FieldDataChange>>, IEnumerable<FieldDataChange>>)(vf => vf.Value))).Where<FieldDataChange>((Func<FieldDataChange, bool>)(f => f.ChangeType != DataChangeType.Unchanged)).Select<FieldDataChange, FieldResult>((Func<FieldDataChange, FieldResult>)(f =>
            {
              switch (f.ChangeType)
              {
                case DataChangeType.Created:
                  return FieldResult.Created(f.FieldId, f.Variance, f.Value);
                case DataChangeType.Updated:
                  return FieldResult.Updated(f.FieldId, f.Variance, f.Value, f.OriginalValue);
                case DataChangeType.Deleted:
                  return FieldResult.Deleted(f.FieldId, f.Variance, f.OriginalValue);
                default:
                  throw new ArgumentOutOfRangeException(string.Format("{0} not supported : {1}.", (object)"DataChangeType", (object)f.ChangeType));
              }
            })).ToArray<FieldResult>()));
          default:
            return (ManifestOperationResult<ItemResult>)null;
        }
      })).Where<ManifestOperationResult<ItemResult>>((Func<ManifestOperationResult<ItemResult>, bool>)(r => r != null)).ToArray<ManifestOperationResult<ItemResult>>()).ConfigureAwait(false);
    }

    protected static ResultChangeType MapChangeType(DataChangeType dataChangeType)
    {
      switch (dataChangeType)
      {
        case DataChangeType.Created:
          return ResultChangeType.Created;
        case DataChangeType.Updated:
          return ResultChangeType.Modified;
        case DataChangeType.Deleted:
          return ResultChangeType.Deleted;
        default:
          throw new ArgumentOutOfRangeException(string.Format("{0} not supported : {1}.", (object)"DataChangeType", (object)dataChangeType));
      }
    }
  }
}
