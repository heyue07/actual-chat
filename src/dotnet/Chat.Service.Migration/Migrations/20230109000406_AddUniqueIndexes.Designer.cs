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
    [Migration("20230109000406_AddUniqueIndexes")]
    partial class AddUniqueIndexes
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("ActualChat.Chat.Db.DbAuthor", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("text")
                        .HasColumnName("id");

                    b.Property<string>("AvatarId")
                        .HasColumnType("text")
                        .HasColumnName("avatar_id");

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

                    b.Property<string>("UserId")
                        .HasColumnType("text")
                        .HasColumnName("user_id");

                    b.Property<long>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("bigint")
                        .HasColumnName("version");

                    b.HasKey("Id")
                        .HasName("pk_authors");

                    b.HasIndex("ChatId", "LocalId")
                        .IsUnique()
                        .HasDatabaseName("ix_authors_chat_id_local_id");

                    b.HasIndex("ChatId", "UserId")
                        .IsUnique()
                        .HasDatabaseName("ix_authors_chat_id_user_id");

                    b.HasIndex("UserId", "AvatarId")
                        .HasDatabaseName("ix_authors_user_id_avatar_id");

                    b.ToTable("authors");
                });

            modelBuilder.Entity("ActualChat.Chat.Db.DbAuthorRole", b =>
                {
                    b.Property<string>("DbAuthorId")
                        .HasColumnType("text")
                        .HasColumnName("author_id");

                    b.Property<string>("DbRoleId")
                        .HasColumnType("text")
                        .HasColumnName("role_id");

                    b.HasKey("DbAuthorId", "DbRoleId")
                        .HasName("pk_author_roles");

                    b.HasIndex("DbRoleId", "DbAuthorId")
                        .IsUnique()
                        .HasDatabaseName("ix_author_roles_role_id_author_id");

                    b.ToTable("author_roles");
                });

            modelBuilder.Entity("ActualChat.Chat.Db.DbChat", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("text")
                        .HasColumnName("id");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<bool>("IsPublic")
                        .HasColumnType("boolean")
                        .HasColumnName("is_public");

                    b.Property<int>("Kind")
                        .HasColumnType("integer")
                        .HasColumnName("kind");

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

            modelBuilder.Entity("ActualChat.Chat.Db.DbChatEntry", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("text")
                        .HasColumnName("id");

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

                    b.Property<bool>("HasReactions")
                        .HasColumnType("boolean")
                        .HasColumnName("has_reactions");

                    b.Property<bool>("IsRemoved")
                        .HasColumnType("boolean")
                        .HasColumnName("is_removed");

                    b.Property<bool>("IsSystemEntry")
                        .HasColumnType("boolean")
                        .HasColumnName("is_system_entry");

                    b.Property<int>("Kind")
                        .HasColumnType("integer")
                        .HasColumnName("kind");

                    b.Property<long>("LocalId")
                        .HasColumnType("bigint")
                        .HasColumnName("local_id");

                    b.Property<long?>("RepliedChatEntryId")
                        .HasColumnType("bigint")
                        .HasColumnName("replied_chat_entry_id");

                    b.Property<string>("StreamId")
                        .HasColumnType("text")
                        .HasColumnName("stream_id");

                    b.Property<string>("TextToTimeMap")
                        .HasColumnType("text")
                        .HasColumnName("text_to_time_map");

                    b.Property<long>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("bigint")
                        .HasColumnName("version");

                    b.Property<long?>("VideoEntryId")
                        .HasColumnType("bigint")
                        .HasColumnName("video_entry_id");

                    b.HasKey("Id")
                        .HasName("pk_chat_entries");

                    b.HasIndex("ChatId", "Kind", "LocalId")
                        .IsUnique()
                        .HasDatabaseName("ix_chat_entries_chat_id_kind_local_id");

                    b.HasIndex("ChatId", "Kind", "Version")
                        .HasDatabaseName("ix_chat_entries_chat_id_kind_version");

                    b.HasIndex("ChatId", "Kind", "BeginsAt", "EndsAt")
                        .HasDatabaseName("ix_chat_entries_chat_id_kind_begins_at_ends_at");

                    b.HasIndex("ChatId", "Kind", "EndsAt", "BeginsAt")
                        .HasDatabaseName("ix_chat_entries_chat_id_kind_ends_at_begins_at");

                    b.HasIndex("ChatId", "Kind", "IsRemoved", "LocalId")
                        .HasDatabaseName("ix_chat_entries_chat_id_kind_is_removed_local_id");

                    b.ToTable("chat_entries");
                });

            modelBuilder.Entity("ActualChat.Chat.Db.DbMention", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("text")
                        .HasColumnName("id");

                    b.Property<string>("ChatId")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("chat_id");

                    b.Property<long>("EntryId")
                        .HasColumnType("bigint")
                        .HasColumnName("entry_id");

                    b.Property<string>("MentionId")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("mention_id");

                    b.HasKey("Id")
                        .HasName("pk_mentions");

                    b.HasIndex("ChatId", "EntryId", "MentionId")
                        .HasDatabaseName("ix_mentions_chat_id_entry_id_mention_id");

                    b.HasIndex("ChatId", "MentionId", "EntryId")
                        .HasDatabaseName("ix_mentions_chat_id_mention_id_entry_id");

                    b.ToTable("mentions");
                });

            modelBuilder.Entity("ActualChat.Chat.Db.DbReaction", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("text")
                        .HasColumnName("id");

                    b.Property<string>("AuthorId")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("author_id");

                    b.Property<string>("EmojiId")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("emoji_id");

                    b.Property<string>("EntryId")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("entry_id");

                    b.Property<DateTime>("ModifiedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("modified_at");

                    b.Property<long>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("bigint")
                        .HasColumnName("version");

                    b.HasKey("Id")
                        .HasName("pk_reactions");

                    b.ToTable("reactions");
                });

            modelBuilder.Entity("ActualChat.Chat.Db.DbReactionSummary", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("text")
                        .HasColumnName("id");

                    b.Property<long>("Count")
                        .HasColumnType("bigint")
                        .HasColumnName("count");

                    b.Property<string>("EmojiId")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("emoji_id");

                    b.Property<string>("EntryId")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("entry_id");

                    b.Property<string>("FirstAuthorIdsJson")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("first_author_ids_json");

                    b.Property<long>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("bigint")
                        .HasColumnName("version");

                    b.HasKey("Id")
                        .HasName("pk_reaction_summaries");

                    b.HasIndex("EntryId")
                        .HasDatabaseName("ix_reaction_summaries_entry_id");

                    b.ToTable("reaction_summaries");
                });

            modelBuilder.Entity("ActualChat.Chat.Db.DbRole", b =>
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

                    b.Property<bool>("CanLeave")
                        .HasColumnType("boolean")
                        .HasColumnName("can_leave");

                    b.Property<bool>("CanRead")
                        .HasColumnType("boolean")
                        .HasColumnName("can_read");

                    b.Property<bool>("CanSeeMembers")
                        .HasColumnType("boolean")
                        .HasColumnName("can_see_members");

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
                        .HasName("pk_roles");

                    b.HasIndex("ChatId", "LocalId")
                        .IsUnique()
                        .HasDatabaseName("ix_roles_chat_id_local_id");

                    b.HasIndex("ChatId", "Name")
                        .HasDatabaseName("ix_roles_chat_id_name");

                    b.ToTable("roles");
                });

            modelBuilder.Entity("ActualChat.Chat.Db.DbTextEntryAttachment", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("text")
                        .HasColumnName("id");

                    b.Property<string>("ContentId")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("content_id");

                    b.Property<string>("EntryId")
                        .IsRequired()
                        .HasColumnType("text")
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

                    b.HasKey("Id")
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

            modelBuilder.Entity("ActualChat.Chat.Db.DbAuthorRole", b =>
                {
                    b.HasOne("ActualChat.Chat.Db.DbAuthor", null)
                        .WithMany("Roles")
                        .HasForeignKey("DbAuthorId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_author_roles_authors_author_id");
                });

            modelBuilder.Entity("ActualChat.Chat.Db.DbAuthor", b =>
                {
                    b.Navigation("Roles");
                });
#pragma warning restore 612, 618
        }
    }
}
