// Copyright (c) Microsoft. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.Its.Domain.Sql.CommandScheduler
{
    [DebuggerStepThrough]
    public class CommandSchedulerDbContext : DbContext
    {
        private static string nameOrConnectionString;

        static CommandSchedulerDbContext()
        {
            Database.SetInitializer(new CommandSchedulerDatabaseInitializer());
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandSchedulerDbContext"/> class.
        /// </summary>
        /// <param name="nameOrConnectionString">Either the database name or a connection string.</param>
        public CommandSchedulerDbContext(string nameOrConnectionString) : base(nameOrConnectionString)
        {
        }

        public virtual DbSet<Clock> Clocks { get; set; }

        public virtual DbSet<ClockMapping> ClockMappings { get; set; }

        public virtual DbSet<ScheduledCommand> ScheduledCommands { get; set; }

        public virtual DbSet<CommandExecutionError> Errors { get; set; }

        public virtual DbSet<ETag> ETags { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Configurations.Add(new ClockEntityTypeConfiguration());
            modelBuilder.Configurations.Add(new ClockMappingsEntityTypeConfiguration());
            modelBuilder.Configurations.Add(new CommandExecutionErrorEntityTypeConfiguration());
            modelBuilder.Configurations.Add(new ScheduledCommandEntityTypeConfiguration());
            modelBuilder.Configurations.Add(new ETagEntityTypeConfiguration());

            // add infrastructure for catchup tracking
            modelBuilder.Configurations.Add(new ReadModelInfoEntityModelConfiguration.ReadModelInfoEntityTypeConfiguration());
            modelBuilder.Configurations.Add(new EventHandlingErrorEntityModelConfiguration.EventHandlingErrorEntityTypeConfiguration());
        }

        private class ScheduledCommandEntityTypeConfiguration : EntityTypeConfiguration<ScheduledCommand>
        {
            public ScheduledCommandEntityTypeConfiguration()
            {
                ToTable("ScheduledCommand", "Scheduler");

                HasKey(c => new { c.AggregateId, c.SequenceNumber });

                HasRequired(c => c.Clock);

                Property(c => c.SerializedCommand)
                    .IsRequired();

                Ignore(c => c.Result);
                Ignore(c => c.NonDurable);
            }
        }

        private class ETagEntityTypeConfiguration : EntityTypeConfiguration<ETag>
        {
            public ETagEntityTypeConfiguration()
            {
                ToTable("ETag", "Scheduler");

                HasKey(c => new { c.Id });

                Property(c => c.Scope)
                    .IsRequired();
                
                Property(c => c.ETagValue)
                    .IsRequired();

                Property(c => c.CreatedDomainTime)
                    .IsRequired();

                Property(c => c.CreatedRealTime)
                    .IsRequired();
            }
        }

        private class ClockEntityTypeConfiguration : EntityTypeConfiguration<Clock>
        {
            public ClockEntityTypeConfiguration()
            {
                ToTable("Clock", "Scheduler");

                Property(c => c.Name)
                    .IsRequired()
                    .HasMaxLength(128);
            }
        }

        private class ClockMappingsEntityTypeConfiguration : EntityTypeConfiguration<ClockMapping>
        {
            public ClockMappingsEntityTypeConfiguration()
            {
                ToTable("ClockMapping", "Scheduler");

                HasRequired(m => m.Clock);

                Property(c => c.Value)
                    .HasMaxLength(128)
                    .IsRequired();
            }
        }

        private class CommandExecutionErrorEntityTypeConfiguration : EntityTypeConfiguration<CommandExecutionError>
        {
            public CommandExecutionErrorEntityTypeConfiguration()
            {
                ToTable("Error", "Scheduler");

                HasRequired(e => e.ScheduledCommand);

                Property(e => e.Error)
                    .IsRequired();
            }
        }
    }
}