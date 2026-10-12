namespace Maydel.Api.Features;

public static class FeatureEndpoints
{
    public static void MapFeatureEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPublicEndpoints();
        app.MapAcademicsEndpoints();
        app.MapStudentEndpoints();
        app.MapStaffEndpoints();
        app.MapAssessmentEndpoints();
        app.MapAttendanceEndpoints();
        app.MapFinanceEndpoints();
        app.MapCommunicationEndpoints();
        app.MapAdmissionsEndpoints();
        app.MapPortalEndpoints();
        app.MapDashboardEndpoints();
        app.MapFileEndpoints();
    }
}
