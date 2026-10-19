using FluentValidation;
using Maydel.Application.Common.Access;
using Maydel.Application.Common.Behaviours;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Maydel.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.Scan(scan => scan.FromAssemblies(assembly)
            .AddClasses(c => c
                .AssignableToAny(typeof(ICommandHandler<>), typeof(ICommandHandler<,>), typeof(IQueryHandler<,>))
                .NotInNamespaceOf(typeof(LoggingDecorator)), publicOnly: false)
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        // Registered innermost first: Logging → Validation → Transaction → handler.
        services.TryDecorate(typeof(ICommandHandler<>), typeof(TransactionDecorator.Command<>));
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(TransactionDecorator.Command<,>));
        services.TryDecorate(typeof(ICommandHandler<>), typeof(ValidationDecorator.Command<>));
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(ValidationDecorator.Command<,>));
        services.TryDecorate(typeof(IQueryHandler<,>), typeof(ValidationDecorator.Query<,>));
        services.TryDecorate(typeof(ICommandHandler<>), typeof(LoggingDecorator.Command<>));
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(LoggingDecorator.Command<,>));
        services.TryDecorate(typeof(IQueryHandler<,>), typeof(LoggingDecorator.Query<,>));

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddScoped<AccessRules>();
        services.AddScoped<SessionService>();
        services.AddScoped<Photos.PhotoStore>();
        services.AddScoped<Reports.ReportBuilder>();
        services.AddScoped<Reports.ReportPdfs>();
        services.AddScoped<Portal.PortalReports>();
        services.AddScoped<Notifications.ReportEmails>();
        services.AddScoped<Attendance.RegisterReader>();
        services.AddScoped<SchoolSettings.SchoolBranding>();
        services.AddScoped<Finance.FinanceDocuments>();
        services.AddScoped<Finance.OnlinePayments>();
        services.AddScoped<Notifications.EmailDispatcher>();
        services.AddScoped<Notifications.InvoiceEmails>();
        services.AddScoped<Notifications.ProofEmails>();
        services.AddScoped<Finance.PaymentProofs>();
        services.AddScoped<Admissions.FormPaid>();
        services.AddScoped<Admissions.AdmissionEmails>();
        services.AddScoped<Admissions.AdmissionLetters>();
        services.AddScoped<Cbt.Tests>();
        services.AddScoped<Cbt.Attempts>();
        services.AddScoped<Cbt.Results>();
        services.AddScoped<Cbt.Gradebook>();
        services.AddScoped<Admissions.FormPayments>();
        return services;
    }
}
