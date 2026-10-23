using FinanceTracker.Contracts.Automation;
using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Contracts.HouseholdMember;
using FinanceTracker.Contracts.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Client.Extensions;

public static class ValidatorRegistration
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// The <c>Contracts.Validation</c> validators the pages' forms use. Listed rather than scanned, so trimming
        /// keeps them. The server registers every validator through the source generator.
        /// </summary>
        public IServiceCollection AddContractValidators()
        {
            services.AddScoped<IValidator<AddClassifications>, AddClassificationsRequestValidator>();
            services.AddScoped<IValidator<AddCustomClassificationsToTransaction>,
                AddCustomClassificationsToTransactionRequestValidator>();
            services.AddScoped<IValidator<AddHouseholdMember>, AddHouseholdMemberRequestValidator>();
            services.AddScoped<IValidator<UpdateCronJob>, CronJobUpdateRequestValidator>();
            return services;
        }
    }
}
