﻿// <auto-generated />
using System;
using ActualChat.Media.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ActualChat.Media.Migrations
{
    [DbContext(typeof(MediaDbContext))]
    [Migration("20230320061421_AddDbOperation")]
    partial class AddDbOperation
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.3")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("ActualChat.Media.Db.DbMedia", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("text")
                        .HasColumnName("id");

                    b.Property<string>("ContentId")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("content_id");

                    b.Property<string>("MetadataJson")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("metadata_json");

                    b.HasKey("Id")
                        .HasName("pk_media");

                    b.ToTable("media");
                });

            modelBuilder.Entity("Stl.Fusion.EntityFramework.Operations.DbOperation", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("text")
                        .HasColumnName("id");

                    b.Property<string>("AgentId")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("agent_id");

                    b.Property<string>("CommandJson")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("command_json");

                    b.Property<DateTime>("CommitTime")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("commit_time");

                    b.Property<string>("ItemsJson")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("items_json");

                    b.Property<DateTime>("StartTime")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("start_time");

                    b.HasKey("Id")
                        .HasName("pk_operations");

                    b.HasIndex(new[] { "CommitTime" }, "IX_CommitTime")
                        .HasDatabaseName("ix_commit_time");

                    b.HasIndex(new[] { "StartTime" }, "IX_StartTime")
                        .HasDatabaseName("ix_start_time");

                    b.ToTable("_operations");
                });
#pragma warning restore 612, 618
        }
    }
}
