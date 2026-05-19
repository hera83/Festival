using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

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
    }
}
