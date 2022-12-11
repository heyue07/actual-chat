﻿using ActualChat.Chat;
using ActualChat.Chat.Module;
using ActualChat.Contacts.Db;
using ActualChat.Db;
using ActualChat.Hosting;
using ActualChat.Users;
using ActualChat.Users.Module;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable MA0004
#pragma warning disable VSTHRD002

namespace ActualChat.Contacts.Migrations
{
    public partial class MoveChats2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            UpAsync(migrationBuilder).Wait();
        }

        private async Task UpAsync(MigrationBuilder migrationBuilder)
        {
            var dbInitializer = DbInitializer.Current as DbInitializer<ContactsDbContext>;
            var chatDbInitializer = dbInitializer.InitializeTasks
                .Select(kv => kv.Key is ChatDbInitializer x ? x : null)
                .SingleOrDefault(x => x != null);
            if (chatDbInitializer == null)
                return;

            var usersDbInitializer = dbInitializer.InitializeTasks
                .Select(kv => kv.Key is UsersDbInitializer x ? x : null)
                .SingleOrDefault(x => x != null);
            if (usersDbInitializer == null)
                return;

            var clocks = dbInitializer.Services.Clocks();
            var versionGenerator = dbInitializer.DbHub.VersionGenerator;

            using var dbContext = dbInitializer.DbHub.CreateDbContext(true);
            using var chatDbContext = chatDbInitializer.DbHub.CreateDbContext();
            using var usersDbContext = usersDbInitializer.DbHub.CreateDbContext();

            // Removing all existing chat DbContacts
            var dbContacts = await dbContext.Contacts
                .Where(c => c.ChatId != null && c.ChatId != "")
                .ToListAsync();
            dbContext.Contacts.RemoveRange(dbContacts);
            await dbContext.SaveChangesAsync();

            // And recreating them
            var dbAccounts = await usersDbContext.Accounts.ToDictionaryAsync(c => (Symbol)c.Id);
            var dbChats = await chatDbContext.Chats.ToDictionaryAsync(c => (Symbol)c.Id);
            var dbAuthors = await chatDbContext.Authors.Where(a => !a.HasLeft).ToListAsync();
            foreach (var dbAuthor in dbAuthors) {
                var userId = new UserId(dbAuthor.UserId, AssumeValid.Option);
                if (userId.IsNone) // Anonymous author, we do nothing in this case
                    continue;

                var chat = dbChats.GetValueOrDefault(dbAuthor.ChatId);
                if (chat == null) // No chat
                    continue;
                if (chat.Kind != ChatKind.Group) // Not a group chat
                    continue;

                var c = new DbContact() {
                    Id = new ContactId(userId, new ChatId(chat.Id), AssumeValid.Option).Value,
                    Version = versionGenerator.NextVersion(),
                    OwnerId = userId.Value,
                    UserId = null,
                    ChatId = chat.Id,
                    TouchedAt = clocks.SystemClock.Now,
                };
                dbContext.Add(c);
            }
            await dbContext.SaveChangesAsync();
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        { }
    }
}
