//  <copyright file="CodeZeroTenantManager.cs" project="CodeZero.Identity.Common" solution="CodeZero">
//      Copyright (c) 2018 CodeZero Framework.  All rights reserved.
//  </copyright>
//  <author>Nasr Aldin M.</author>
//  <email>nasr2ldin@gmail.com</email>
//  <website>https://nasraldin.com/codezero</website>
//  <github>https://nasraldin.github.io/CodeZero</github>
//  <date>01/01/2018 01:00 AM</date>
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CodeZero.Application.Editions;
using CodeZero.Application.Features;
using CodeZero.Authorization.Users;
using CodeZero.Collections.Extensions;
using CodeZero.Domain.Repositories;
using CodeZero.Domain.Services;
using CodeZero.Domain.Uow;
using CodeZero.Events.Bus.Entities;
using CodeZero.Events.Bus.Handlers;
using CodeZero.Localization;
using CodeZero.Runtime.Caching;
using CodeZero.UI;
using CodeZero.Identity;

namespace CodeZero.MultiTenancy
{
    /// <summary>
    /// Tenant manager.
    /// Implements domain logic for <see cref="CodeZeroTenant{TUser}"/>.
    /// </summary>
    /// <typeparam name="TTenant">Type of the application Tenant</typeparam>
    /// <typeparam name="TUser">Type of the application User</typeparam>
    public class CodeZeroTenantManager<TTenant, TUser> : IDomainService,
        IEventHandler<EntityChangedEventData<TTenant>>,
        IEventHandler<EntityDeletedEventData<Edition>>
        where TTenant : CodeZeroTenant<TUser>
        where TUser : CodeZeroUserBase
    {
        public CodeZeroEditionManager EditionManager { get; set; }

        public ILocalizationManager LocalizationManager { get; set; }

        public ICacheManager CacheManager { get; set; }

        public IFeatureManager FeatureManager { get; set; }

        public IUnitOfWorkManager UnitOfWorkManager { get; set; }

        protected IRepository<TTenant> TenantRepository { get; set; }

        protected IRepository<TenantFeatureSetting, long> TenantFeatureRepository { get; set; }

        private readonly ICodeZeroFeatureValueStore _featureValueStore;

        public CodeZeroTenantManager(
            IRepository<TTenant> tenantRepository, 
            IRepository<TenantFeatureSetting, long> tenantFeatureRepository,
            CodeZeroEditionManager editionManager,
            ICodeZeroFeatureValueStore featureValueStore)
        {
            _featureValueStore = featureValueStore;
            TenantRepository = tenantRepository;
            TenantFeatureRepository = tenantFeatureRepository;
            EditionManager = editionManager;
            LocalizationManager = NullLocalizationManager.Instance;
        }

        public virtual IQueryable<TTenant> Tenants { get { return TenantRepository.GetAll(); } }

        public virtual async Task CreateAsync(TTenant tenant)
        {
            await ValidateTenantAsync(tenant);

            if (await TenantRepository.FirstOrDefaultAsync(t => t.TenancyName == tenant.TenancyName) != null)
            {
                throw new UserFriendlyException(string.Format(L("TenancyNameIsAlreadyTaken"), tenant.TenancyName));
            }

            await TenantRepository.InsertAsync(tenant);
        }

        public virtual async Task UpdateAsync(TTenant tenant)
        {
            if (await TenantRepository.FirstOrDefaultAsync(t => t.TenancyName == tenant.TenancyName && t.Id != tenant.Id) != null)
            {
                throw new UserFriendlyException(string.Format(L("TenancyNameIsAlreadyTaken"), tenant.TenancyName));
            }

            await TenantRepository.UpdateAsync(tenant);
        }

        public virtual async Task<TTenant> FindByIdAsync(int id)
        {
            return await TenantRepository.FirstOrDefaultAsync(id);
        }

        public virtual async Task<TTenant> GetByIdAsync(int id)
        {
            var tenant = await FindByIdAsync(id);
            if (tenant == null)
            {
                throw new CodeZeroException("There is no tenant with id: " + id);
            }

            return tenant;
        }

        public virtual Task<TTenant> FindByTenancyNameAsync(string tenancyName)
        {
            return TenantRepository.FirstOrDefaultAsync(t => t.TenancyName == tenancyName);
        }

        public virtual async Task DeleteAsync(TTenant tenant)
        {
            await TenantRepository.DeleteAsync(tenant);
        }

        public Task<string> GetFeatureValueOrNullAsync(int tenantId, string featureName)
        {
            return _featureValueStore.GetValueOrNullAsync(tenantId, featureName);
        }

        public virtual async Task<IReadOnlyList<NameValue>> GetFeatureValuesAsync(int tenantId)
        {
            var values = new List<NameValue>();

            foreach (var feature in FeatureManager.GetAll())
            {
                values.Add(new NameValue(feature.Name, await GetFeatureValueOrNullAsync(tenantId, feature.Name) ?? feature.DefaultValue));
            }

            return values;
        }

        public virtual async Task SetFeatureValuesAsync(int tenantId, params NameValue[] values)
        {
            if (values.IsNullOrEmpty())
            {
                return;
            }

            foreach (var value in values)
            {
                await SetFeatureValueAsync(tenantId, value.Name, value.Value);
            }
        }

        [UnitOfWork]
        public virtual async Task SetFeatureValueAsync(int tenantId, string featureName, string value)
        {
            await SetFeatureValueAsync(await GetByIdAsync(tenantId), featureName, value);
        }

        [UnitOfWork]
        public virtual async Task SetFeatureValueAsync(TTenant tenant, string featureName, string value)
        {
            //No need to change if it's already equals to the current value
            if (await GetFeatureValueOrNullAsync(tenant.Id, featureName) == value)
            {
                return;
            }

            //Get the current feature setting
            TenantFeatureSetting currentSetting;
            using (UnitOfWorkManager.Current.SetTenantId(tenant.Id))
            {
                currentSetting = await TenantFeatureRepository.FirstOrDefaultAsync(f => f.Name == featureName);
            }

            //Get the feature
            var feature = FeatureManager.GetOrNull(featureName);
            if (feature == null)
            {
                if (currentSetting != null)
                {
                    await TenantFeatureRepository.DeleteAsync(currentSetting);
                }

                return;
            }

            //Determine default value
            var defaultValue = tenant.EditionId.HasValue
                ? (await EditionManager.GetFeatureValueOrNullAsync(tenant.EditionId.Value, featureName) ?? feature.DefaultValue)
                : feature.DefaultValue;

            //No need to store value if it's default
            if (value == defaultValue)
            {
                if (currentSetting != null)
                {
                    await TenantFeatureRepository.DeleteAsync(currentSetting);
                }

                return;
            }

            //Insert/update the feature value
            if (currentSetting == null)
            {
                await TenantFeatureRepository.InsertAsync(new TenantFeatureSetting(tenant.Id, featureName, value));
            }
            else
            {
                currentSetting.Value = value;
            }
        }

        /// <summary>
        /// Resets all custom feature settings for a tenant.
        /// Tenant will have features according to it's edition.
        /// </summary>
        /// <param name="tenantId">Tenant Id</param>
        [UnitOfWork]
        public virtual async Task ResetAllFeaturesAsync(int tenantId)
        {
            using (UnitOfWorkManager.Current.SetTenantId(tenantId))
            {
                await TenantFeatureRepository.DeleteAsync(f => f.TenantId == tenantId);
            }
        }

        protected virtual async Task ValidateTenantAsync(TTenant tenant)
        {
            await ValidateTenancyNameAsync(tenant.TenancyName);
        }

        protected virtual Task ValidateTenancyNameAsync(string tenancyName)
        {
            if (!Regex.IsMatch(tenancyName, CodeZeroTenant<TUser>.TenancyNameRegex))
            {
                throw new UserFriendlyException(L("InvalidTenancyName"));
            }

            return Task.FromResult(0);
        }

        private string L(string name)
        {
            return LocalizationManager.GetString(CodeZeroConsts.LocalizationSourceName, name);
        }

        public void HandleEvent(EntityChangedEventData<TTenant> eventData)
        {
            if (eventData.Entity.IsTransient())
            {
                return;
            }

            CacheManager.GetTenantFeatureCache().Remove(eventData.Entity.Id);
        }

        [UnitOfWork]
        public virtual void HandleEvent(EntityDeletedEventData<Edition> eventData)
        {
            var relatedTenants = TenantRepository.GetAllList(t => t.EditionId == eventData.Entity.Id);
            foreach (var relatedTenant in relatedTenants)
            {
                relatedTenant.EditionId = null;
            }
        }
    }
}