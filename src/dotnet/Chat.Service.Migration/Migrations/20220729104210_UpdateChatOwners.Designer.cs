﻿// <auto-generated />
using System;
using ActualChat.Chat.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ActualChat.Chat.Migrations
{
    [DbContext(typeof(ChatDbContext))]
    [Migration("20220729104210_UpdateChatOwners")]
    partial class UpdateChatOwners
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.6")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("ActualChat.Chat.Db.DbChat", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("text")
                        .HasColumnName("id");

                    b.Property<int>("ChatType")
                        .HasColumnType("integer")
                        .HasColumnName("chat_type");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<bool>("IsPublic")
                        .HasColumnType("boolean")
                        .HasColumnName("is_public");

                    b.Property<string>("Picture")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("picture");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("title");

                    b.Property<long>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("bigint")
                        .HasColumnName("version");

                    b.HasKey("Id")
                        .HasName("pk_chats");

                    b.ToTable("chats");
                });

            modelBuilder.Entity("ActualChat.Chat.Db.DbChatAuthor", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("text")
                        .HasColumnName("id");

                    b.Property<string>("ChatId")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("chat_id");

                    b.Property<bool>("HasLeft")
                        .HasColumnType("boolean")
                        .HasColumnName("has_left");

                    b.Property<bool>("IsAnonymous")
                        .HasColumnType("boolean")
                        .HasColumnName("is_anonymous");

                    b.Property<long>("LocalId")
                        .HasColumnType("bigint")
                        .HasColumnName("local_id");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("name");

                    b.Property<string>("UserId")
                        .HasColumnType("text")
                        .HasColumnName("user_id");

                    b.Property<long>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("bigint")
                        .HasColumnName("version");

                    b.HasKey("Id")
                        .HasName("pk_chat_authors");

                    b.HasIndex("ChatId", "LocalId")
                        .HasDatabaseName("ix_chat_authors_chat_id_local_id");

                    b.HasIndex("ChatId", "UserId")
                        .HasDatabaseName("ix_chat_authors_chat_id_user_id");

                    b.ToTable("chat_authors");
                });

            modelBuilder.Entity("ActualChat.Chat.Db.DbChatAuthorRole", b =>
                {
                    b.Property<string>("DbChatAuthorId")
                        .HasColumnType("text")
                        .HasColumnName("chat_author_id");

                    b.Property<string>("DbChatRoleId")
                        .HasColumnType("text")
                        .HasColumnName("chat_role_id");

                    b.HasKey("DbChatAuthorId", "DbChatRoleId")
                        .HasName("pk_chat_author_roles");

                    b.HasIndex("DbChatRoleId", "DbChatAuthorId")
                        .IsUnique()
                        .HasDatabaseName("ix_chat_author_roles_chat_role_id_chat_author_id");

                    b.ToTable("chat_author_roles");
                });

            modelBuilder.Entity("ActualChat.Chat.Db.DbChatEntry", b =>
                {
                    b.Property<string>("CompositeId")
                        .HasColumnType("text")
                        .HasColumnName("composite_id");

                    b.Property<long?>("AudioEntryId")
                        .HasColumnType("bigint")
                        .HasColumnName("audio_entry_id");

                    b.Property<string>("AuthorId")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("author_id");

                    b.Property<DateTime>("BeginsAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("begins_at");

                    b.Property<string>("ChatId")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("chat_id");

                    b.Property<DateTime?>("ClientSideBeginsAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("client_side_begins_at");

                    b.Property<string>("Content")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("content");

                    b.Property<DateTime?>("ContentEndsAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("content_ends_at");

                    b.Property<double>("Duration")
                        .HasColumnType("double precision")
                        .HasColumnName("duration");

                    b.Property<DateTime?>("EndsAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("ends_at");

                    b.Property<bool>("HasAttachments")
                        .HasColumnType("boolean")
                        .HasColumnName("has_attachments");

                    b.Property<long>("Id")
                        .HasColumnType("bigint")
                        .HasColumnName("id");

                    b.Property<bool>("IsRemoved")
                        .HasColumnType("boolean")
                        .HasColumnName("is_removed");

                    b.Property<long?>("RepliedChatEntryId")
                        .HasColumnType("bigint")
                        .HasColumnName("replied_chat_entry_id");

                    b.Property<string>("StreamId")
                        .HasColumnType("text")
                        .HasColumnName("stream_id");

                    b.Property<string>("TextToTimeMap")
                        .HasColumnType("text")
                        .HasColumnName("text_to_time_map");

                    b.Property<int>("Type")
                        .HasColumnType("integer")
                        .HasColumnName("type");

                    b.Property<long>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("bigint")
                        .HasColumnName("version");

                    b.Property<long?>("VideoEntryId")
                        .HasColumnType("bigint")
                        .HasColumnName("video_entry_id");

                    b.HasKey("CompositeId")
                        .HasName("pk_chat_entries");

                    b.HasIndex("ChatId", "Type", "Id")
                        .HasDatabaseName("ix_chat_entries_chat_id_type_id");

                    b.HasIndex("ChatId", "Type", "Version")
                        .HasDatabaseName("ix_chat_entries_chat_id_type_version");

                    b.HasIndex("ChatId", "Type", "BeginsAt", "EndsAt")
                        .HasDatabaseName("ix_chat_entries_chat_id_type_begins_at_ends_at");

                    b.HasIndex("ChatId", "Type", "EndsAt", "BeginsAt")
                        .HasDatabaseName("ix_chat_entries_chat_id_type_ends_at_begins_at");

                    b.HasIndex("ChatId", "Type", "IsRemoved", "Id")
                        .HasDatabaseName("ix_chat_entries_chat_id_type_is_removed_id");

                    b.ToTable("chat_entries");
                });

            modelBuilder.Entity("ActualChat.Chat.Db.DbChatOwner", b =>
                {
                    b.Property<string>("DbChatId")
                        .HasColumnType("text")
                        .HasColumnName("chat_id");

                    b.Property<string>("DbUserId")
                        .HasColumnType("text")
                        .HasColumnName("user_id");

                    b.HasKey("DbChatId", "DbUserId")
                        .HasName("pk_chat_owners");

                    b.ToTable("chat_owners");
                });

            modelBuilder.Entity("ActualChat.Chat.Db.DbChatRole", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("text")
                        .HasColumnName("id");

                    b.Property<bool>("CanEditProperties")
                        .HasColumnType("boolean")
                        .HasColumnName("can_edit_properties");

                    b.Property<bool>("CanEditRoles")
                        .HasColumnType("boolean")
                        .HasColumnName("can_edit_roles");

                    b.Property<bool>("CanInvite")
                        .HasColumnType("boolean")
                        .HasColumnName("can_invite");

                    b.Property<bool>("CanJoin")
                        .HasColumnType("boolean")
                        .HasColumnName("can_join");

                    b.Property<bool>("CanRead")
                        .HasColumnType("boolean")
                        .HasColumnName("can_read");

                    b.Property<bool>("CanWrite")
                        .HasColumnType("boolean")
                        .HasColumnName("can_write");

                    b.Property<string>("ChatId")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("chat_id");

                    b.Property<long>("LocalId")
                        .HasColumnType("bigint")
                        .HasColumnName("local_id");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("name");

                    b.Property<string>("Picture")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("picture");

                    b.Property<short>("SystemRole")
                        .HasColumnType("smallint")
                        .HasColumnName("system_role");

                    b.Property<long>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("bigint")
                        .HasColumnName("version");

                    b.HasKey("Id")
                        .HasName("pk_chat_roles");

                    b.HasIndex("ChatId", "LocalId")
                        .HasDatabaseName("ix_chat_roles_chat_id_local_id");

                    b.HasIndex("ChatId", "Name")
                        .HasDatabaseName("ix_chat_roles_chat_id_name");

                    b.ToTable("chat_roles");
                });

            modelBuilder.Entity("ActualChat.Chat.Db.DbTextEntryAttachment", b =>
                {
                    b.Property<string>("CompositeId")
                        .HasColumnType("text")
                        .HasColumnName("composite_id");

                    b.Property<string>("ChatId")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("chat_id");

                    b.Property<string>("ContentId")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("content_id");

                    b.Property<long>("EntryId")
                        .HasColumnType("bigint")
                        .HasColumnName("entry_id");

                    b.Property<int>("Index")
                        .HasColumnType("integer")
                        .HasColumnName("index");

                    b.Property<string>("MetadataJson")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("metadata_json");

                    b.Property<long>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("bigint")
                        .HasColumnName("version");

                    b.HasKey("CompositeId")
                        .HasName("pk_text_entry_attachments");

                    b.ToTable("text_entry_attachments");
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

            modelBuilder.Entity("ActualChat.Chat.Db.DbChatAuthorRole", b =>
                {
                    b.HasOne("ActualChat.Chat.Db.DbChatAuthor", null)
                        .WithMany("Roles")
                        .HasForeignKey("DbChatAuthorId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_chat_author_roles_chat_authors_chat_author_id");
                });

            modelBuilder.Entity("ActualChat.Chat.Db.DbChatOwner", b =>
                {
                    b.HasOne("ActualChat.Chat.Db.DbChat", null)
                        .WithMany("Owners")
                        .HasForeignKey("DbChatId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_chat_owners_chats_chat_id");
                });

            modelBuilder.Entity("ActualChat.Chat.Db.DbChat", b =>
                {
                    b.Navigation("Owners");
                });

            modelBuilder.Entity("ActualChat.Chat.Db.DbChatAuthor", b =>
                {
                    b.Navigation("Roles");
                });
#pragma warning restore 612, 618
        }
    }
}
