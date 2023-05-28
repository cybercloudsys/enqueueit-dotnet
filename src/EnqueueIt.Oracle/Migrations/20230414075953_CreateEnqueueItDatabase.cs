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

using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EnqueueIt.Oracle.Migrations
{
    public partial class CreateEnqueueItDatabase : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "jobs",
                columns: table => new
                {
                    id = table.Column<string>(type: "CHAR(36)", nullable: false),
                    name = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    queue = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    app_name = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    argument = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    created_at = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    is_recurring = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    start_at = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    active = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    recurring = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    tries = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    after_background_job_ids = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    type = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_jobs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "background_jobs",
                columns: table => new
                {
                    id = table.Column<string>(type: "CHAR(36)", nullable: false),
                    job_id = table.Column<string>(type: "CHAR(36)", nullable: false),
                    processed_by = table.Column<string>(type: "CHAR(36)", nullable: true),
                    server = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    created_at = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    job_error = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    started_at = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    completed_at = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    last_activity = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    logs = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_background_jobs", x => x.id);
                    table.ForeignKey(
                        name: "fk_background_jobs_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_background_jobs_job_id",
                table: "background_jobs",
                column: "job_id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "background_jobs");

            migrationBuilder.DropTable(
                name: "jobs");
        }
    }
}
