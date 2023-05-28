// EnqueueIt
// Copyright © 2023 Cyber Cloud Systems LLC

// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.

// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using Microsoft.AspNetCore.Builder;

namespace EnqueueIt
{
    public static class EnqueueItBuilderExtensions
    {
        public static IApplicationBuilder UseEnqueueItDashboard(this IApplicationBuilder app, string routePrefix = null)
        {
            var x = app.ApplicationServices.GetService(typeof(GlobalConfiguration));
            if (string.IsNullOrWhiteSpace(routePrefix))
                routePrefix = "/EnqueueIt";
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "EnqueueIt",
                    pattern: routePrefix + "/{action=Index}/{id?}",
                    defaults: new { controller = "EnqueueIt" });
            });
            app.UseStaticFiles();
            return app;
        }
    }
}