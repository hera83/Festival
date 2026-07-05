using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using web.Models;

namespace web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Volunteer> Volunteers => Set<Volunteer>();
    public DbSet<ShiftType> ShiftTypes => Set<ShiftType>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<VolunteerCheckIn> VolunteerCheckIns => Set<VolunteerCheckIn>();
    public DbSet<VolunteerLocationLog> VolunteerLocationLogs => Set<VolunteerLocationLog>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<DashboardSetting> DashboardSettings => Set<DashboardSetting>();
    public DbSet<UserCameraPreference> UserCameraPreferences => Set<UserCameraPreference>();
    public DbSet<VolunteerMeta> VolunteerMetas => Set<VolunteerMeta>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageAttachment> MessageAttachments => Set<MessageAttachment>();
    public DbSet<MessageTask> MessageTasks => Set<MessageTask>();
    public DbSet<MessageReply> MessageReplies => Set<MessageReply>();
    public DbSet<VolunteerGpsLog> VolunteerGpsLogs => Set<VolunteerGpsLog>();
    public DbSet<MapLocation> MapLocations => Set<MapLocation>();
    public DbSet<ScheduledMove> ScheduledMoves => Set<ScheduledMove>();
    public DbSet<SmsMessage> SmsMessages => Set<SmsMessage>();
    public DbSet<SmsTemplate> SmsTemplates => Set<SmsTemplate>();
    public DbSet<NoShowSnooze> NoShowSnoozes => Set<NoShowSnooze>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Global fix: SQLite gemmer DateTime som tekst og konverterer Kind=Local til UTC.
        // Denne converter sikrer at ALLE DateTime-kolonner gemmes og læses som Unspecified,
        // så SQLite aldrig forsøger at konvertere tidszoner.
        var dateTimeConverter = new ValueConverter<DateTime, DateTime>(
            v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified),
            v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified)
        );
        var nullableDateTimeConverter = new ValueConverter<DateTime?, DateTime?>(
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Unspecified) : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Unspecified) : v
        );
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                    property.SetValueConverter(dateTimeConverter);
                else if (property.ClrType == typeof(DateTime?))
                    property.SetValueConverter(nullableDateTimeConverter);
            }
        }

        builder.Entity<Volunteer>(entity =>
        {
            entity.HasIndex(x => new { x.SeasonId, x.Key }).IsUnique();
            entity.Property(x => x.Key).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(256);
            entity.Property(x => x.PhoneNumber).HasMaxLength(32);
            entity.Property(x => x.QrToken).HasMaxLength(128).IsRequired();
            entity.Property(x => x.QrCodeSentBy).HasMaxLength(450);
        });

        builder.Entity<ShiftType>(entity =>
        {
            entity.HasIndex(x => new { x.SeasonId, x.ShiftName, x.StartTime, x.EndTime }).IsUnique();
            entity.Property(x => x.ShiftName).HasMaxLength(256).IsRequired();
        });

        builder.Entity<Shift>(entity =>
        {
            entity.HasOne(x => x.Volunteer)
                .WithMany(x => x.Shifts)
                .HasForeignKey(x => x.VolunteerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.ShiftType)
                .WithMany(x => x.Shifts)
                .HasForeignKey(x => x.ShiftTypeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.SeasonId, x.VolunteerId });
            entity.HasIndex(x => new { x.SeasonId, x.ShiftTypeId });
        });

        builder.Entity<VolunteerCheckIn>(entity =>
        {
            entity.HasIndex(x => new { x.SeasonId, x.VolunteerId, x.CheckInDate });
            entity.HasIndex(x => new { x.SeasonId, x.CheckInDate });

            entity.HasOne(x => x.Volunteer)
                .WithMany()
                .HasForeignKey(x => x.VolunteerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(x => x.CurrentLocation).HasMaxLength(64).IsRequired();
        });

        builder.Entity<VolunteerLocationLog>(entity =>
        {
            entity.HasIndex(x => new { x.SeasonId, x.VolunteerId });
            entity.HasIndex(x => x.CheckInId);

            entity.HasOne(x => x.CheckIn)
                .WithMany(x => x.LocationLogs)
                .HasForeignKey(x => x.CheckInId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Volunteer)
                .WithMany()
                .HasForeignKey(x => x.VolunteerId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.Property(x => x.EventType).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Location).HasMaxLength(64);
        });

        builder.Entity<Post>(entity =>
        {
            entity.HasIndex(x => new { x.SeasonId, x.Name }).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ColumnIndex).IsRequired();
            entity.Property(x => x.SortOrder).IsRequired();
        });

        builder.Entity<DashboardSetting>(entity =>
        {
            entity.HasIndex(x => new { x.SeasonId, x.Key }).IsUnique();
            entity.Property(x => x.Key).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Value).HasMaxLength(256);
        });

        builder.Entity<UserCameraPreference>(entity =>
        {
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.Property(x => x.UserId).HasMaxLength(450).IsRequired();
            entity.Property(x => x.DeviceId).HasMaxLength(256).IsRequired();
            entity.Property(x => x.DeviceFingerprint).HasMaxLength(2048).IsRequired();
        });

        builder.Entity<VolunteerMeta>(entity =>
        {
            entity.HasIndex(x => x.VolunteerId).IsUnique();
            entity.HasOne(x => x.Volunteer)
                .WithMany()
                .HasForeignKey(x => x.VolunteerId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Property(x => x.AppConfirmCode).HasMaxLength(6);
        });

        builder.Entity<Message>(entity =>
        {
            entity.HasIndex(x => new { x.SeasonId, x.VolunteerId });
            entity.HasIndex(x => new { x.SeasonId, x.IsRead });
            entity.Property(x => x.SentByUserId).HasMaxLength(450).IsRequired();
            entity.Property(x => x.Subject).HasMaxLength(512).IsRequired();
            entity.Property(x => x.Body).IsRequired();
            entity.HasOne(x => x.Volunteer)
                .WithMany()
                .HasForeignKey(x => x.VolunteerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<MessageAttachment>(entity =>
        {
            entity.Property(x => x.OriginalFileName).HasMaxLength(256).IsRequired();
            entity.Property(x => x.StoredFileName).HasMaxLength(256).IsRequired();
            entity.Property(x => x.ContentType).HasMaxLength(128).IsRequired();
            entity.Property(x => x.UploadedByUserId).HasMaxLength(450).IsRequired();
            entity.HasOne(x => x.Message)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<MessageTask>(entity =>
        {
            entity.Property(x => x.Title).HasMaxLength(512).IsRequired();
            entity.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
            entity.Property(x => x.CompletedByUserId).HasMaxLength(450);
            entity.HasOne(x => x.Message)
                .WithMany(x => x.Tasks)
                .HasForeignKey(x => x.MessageId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<MessageReply>(entity =>
        {
            entity.Property(x => x.Body).IsRequired();
            entity.Property(x => x.SentByUserId).HasMaxLength(450);
            entity.HasOne(x => x.Message)
                .WithMany(x => x.Replies)
                .HasForeignKey(x => x.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<VolunteerGpsLog>(entity =>
        {
            entity.HasIndex(x => new { x.SeasonId, x.VolunteerId });
            entity.HasIndex(x => x.LoggedAt);
            entity.Property(x => x.VolunteerKey).HasMaxLength(128).IsRequired();
            entity.Property(x => x.VolunteerName).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Trigger).HasMaxLength(64).IsRequired();
            entity.HasOne(x => x.Volunteer)
                .WithMany()
                .HasForeignKey(x => x.VolunteerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<MapLocation>(entity =>
        {
            entity.HasIndex(x => x.SeasonId);
            entity.Property(x => x.Name).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1024);
        });

        builder.Entity<SmsMessage>(entity =>
        {
            entity.HasIndex(x => x.MessageId).IsUnique();
            entity.HasIndex(x => new { x.SeasonId, x.VolunteerId });
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => x.Direction);

            // Ingen HasDefaultValue her: Direction sættes altid eksplicit ved skrivning
            // (SmsMessageLogService for udgående, SmsWebhookController for indgående).
            // Et databasedefault ville kollidere med at Inbound=0 er enummets CLR-default.
            entity.Property(x => x.Direction).HasConversion<int>();
            entity.Property(x => x.PhoneNumberSnapshot).HasMaxLength(32).IsRequired();
            entity.Property(x => x.MessageBody).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.SentByUserId).HasMaxLength(450);
            entity.Property(x => x.IsReadByCoordinator).HasDefaultValue(true);

            entity.HasOne(x => x.Volunteer)
                .WithMany()
                .HasForeignKey(x => x.VolunteerId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<SmsTemplate>(entity =>
        {
            entity.HasIndex(x => new { x.SeasonId, x.Type }).IsUnique();
            entity.Property(x => x.Type).HasConversion<int>();
            entity.Property(x => x.Body).HasMaxLength(1000).IsRequired();
        });

        builder.Entity<NoShowSnooze>(entity =>
        {
            entity.HasIndex(x => new { x.SeasonId, x.VolunteerId }).IsUnique();
            entity.Property(x => x.CreatedByUser).HasMaxLength(450).IsRequired();

            entity.HasOne(x => x.Volunteer)
                .WithMany()
                .HasForeignKey(x => x.VolunteerId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
