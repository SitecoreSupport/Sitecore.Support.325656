namespace Sitecore.Support.Framework.Publishing.DataPromotion
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Threading;
  using System.Threading.Tasks;
  using Microsoft.Extensions.Configuration;
  using Microsoft.Extensions.Logging;
  using Sitecore.Framework.Conditions;
  using Sitecore.Framework.Publishing.DataPromotion;
  using Sitecore.Framework.Publishing.Item;
  using Sitecore.Framework.Publishing.Locators;
  using Sitecore.Framework.Publishing.Manifest;
  public class DefaultItemManifestPromoter : ManifestPromoterBase, IItemManifestPromoter
  {
    protected readonly PromoterOptions _options;

    public DefaultItemManifestPromoter(
        ILogger<DefaultItemManifestPromoter> logger,
        PromoterOptions options = null) : base(logger)
    {
      Condition.Requires(logger, nameof(logger)).IsNotNull();

      _options = options ?? new PromoterOptions();
    }

    public DefaultItemManifestPromoter(
        ILogger<DefaultItemManifestPromoter> logger,
        IConfiguration config) : this(
            logger,
            config.As<PromoterOptions>())
    {
    }

    public virtual async Task Promote(
        IManifestRepository manifestRepository,
        TargetPromoteContext targetContext,
        ICompositeItemReadRepository sourceItemRepository,
        IItemRelationshipRepository relationshipRepository,
        IItemWriteRepository targetItemRepository,
        FieldReportSpecification fieldsToReport,
        CancellationTokenSource cancelTokenSource)
    {
      cancelTokenSource.Token.ThrowIfCancellationRequested();

      await base.Promote(async () =>
      {
        var itemWorker = CreatePromoteWorker(manifestRepository, targetItemRepository, targetContext.Manifest.ManifestId, targetContext.CalculateResults, fieldsToReport);

        await ProcessManifestInBatches(
            manifestRepository,
            targetContext.Manifest.ManifestId,
            ManifestStepAction.DeleteItem,
            async (IItemLocator[] uriBatch) =>
            {
              var ids = uriBatch.Select(uri => uri.Id).ToArray();
              await Task.WhenAll(
                          itemWorker.DeleteItems(ids),
                          relationshipRepository.Delete(targetContext.TargetStore.ScDatabaseName, ids)).ConfigureAwait(false);
            },
            _options.BatchSize,
            cancelTokenSource).ConfigureAwait(false);

        await ProcessManifestInBatches(
            manifestRepository,
            targetContext.Manifest.ManifestId,
            ManifestStepAction.DeleteItemVariant,
            async (IItemVariantLocator[] uriBatch) =>
            {
              await Task.WhenAll(
                          itemWorker.DeleteVariants(uriBatch),
                          relationshipRepository.Delete(targetContext.TargetStore.ScDatabaseName, uriBatch)).ConfigureAwait(false);
            },
            _options.BatchSize,
            cancelTokenSource).ConfigureAwait(false);

        await ProcessManifestInBatches(
            manifestRepository,
            targetContext.Manifest.ManifestId,
            ManifestStepAction.PromoteItemVariant,
            async (IItemVariantLocator[] uriBatch) =>
            {
              var variantsTask = sourceItemRepository.GetVariants(uriBatch);
              var relsTask = relationshipRepository.GetOutRelationships(targetContext.SourceStore.ScDatabaseName, uriBatch);
              await Task.WhenAll(variantsTask, relsTask).ConfigureAwait(false);

              return new Tuple<IItemVariant[], IDictionary<IItemVariantIdentifier, IReadOnlyCollection<IItemRelationship>>>(variantsTask.Result.ToArray(), relsTask.Result);
            },
            async sourceData =>
            {
              if (!sourceData.Item1.Any()) return;

              await Task.WhenAll(
                          itemWorker.SaveVariants(sourceData.Item1),
                          relationshipRepository.Save(targetContext.TargetStore.ScDatabaseName, sourceData.Item2)).ConfigureAwait(false);
            },
            _options.BatchSize,
            cancelTokenSource).ConfigureAwait(false);
      },
      cancelTokenSource);
    }

    protected virtual IItemPromoteWorker CreatePromoteWorker(IManifestRepository manifestRepository, IItemWriteRepository targetRepo, Guid manifestId, bool calculateResults, FieldReportSpecification fieldsToReport)
    {
      return new ItemManifestPromoteWorker(manifestRepository, targetRepo, manifestId, calculateResults, fieldsToReport);
    }
  }
}