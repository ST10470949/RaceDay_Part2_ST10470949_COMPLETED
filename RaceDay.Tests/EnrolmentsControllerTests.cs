using Microsoft.AspNetCore.Mvc;
using RaceDay.API.Controllers;
using RaceDay.API.DTOs.Enrolments;
using RaceDay.API.Models;
using RaceDay.Tests.TestHelpers;
using Xunit;

namespace RaceDay.Tests
{
    public class EnrolmentsControllerTests
    {
        private static (User organiser, User participant, Category category) SeedRace(RaceDay.API.Data.RaceDayContext context)
        {
            var organiser = new User
            {
                FullName = "Johan van der Merwe",
                Email = "johan@raceday.co.za",
                PasswordHash = "irrelevant-for-this-test",
                Role = UserRole.Organiser
            };

            var participant = new User
            {
                FullName = "Sipho Nkosi",
                Email = "sipho@example.com",
                PasswordHash = "irrelevant-for-this-test",
                Role = UserRole.Participant
            };

            context.Users.AddRange(organiser, participant);
            context.SaveChanges();

            var raceEvent = new Event
            {
                OrganiserID = organiser.UserID,
                Name = "Soweto Marathon",
                EventDate = DateTime.Today.AddMonths(1),
                Location = "Soweto"
            };
            context.Events.Add(raceEvent);
            context.SaveChanges();

            var category = new Category
            {
                EventID = raceEvent.EventID,
                Name = "10km Fun Run",
                EntryFee = 150,
                MaxParticipants = 2
            };
            context.Categories.Add(category);
            context.SaveChanges();

            return (organiser, participant, category);
        }

        [Fact]
        public async Task Participant_CanEnrol_AndEnrolmentIsCorrectlyLinked()
        {
            var context = TestDbFactory.CreateContext();
            var (_, participant, category) = SeedRace(context);

            var controller = new EnrolmentsController(context)
            {
                ControllerContext = TestDbFactory.BuildControllerContext(participant.UserID, "Participant")
            };

            var result = await controller.Enrol(new CreateEnrolmentDto { CategoryId = category.CategoryID });

            var created = Assert.IsType<ObjectResult>(result);
            Assert.Equal(201, created.StatusCode);

            var savedEnrolment = Assert.Single(context.Enrolments);
            Assert.Equal(participant.UserID, savedEnrolment.ParticipantID);
            Assert.Equal(category.CategoryID, savedEnrolment.CategoryID);
        }

        [Fact]
        public async Task Organiser_CannotEnrol_ReturnsForbidden()
        {
            var context = TestDbFactory.CreateContext();
            var (organiser, _, category) = SeedRace(context);

            var controller = new EnrolmentsController(context)
            {
                ControllerContext = TestDbFactory.BuildControllerContext(organiser.UserID, "Organiser")
            };

            var result = await controller.Enrol(new CreateEnrolmentDto { CategoryId = category.CategoryID });

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(403, objectResult.StatusCode);
            Assert.Empty(context.Enrolments);
        }

        [Fact]
        public async Task Participant_CannotEnrolTwiceInSameCategory_ReturnsConflict()
        {
            var context = TestDbFactory.CreateContext();
            var (_, participant, category) = SeedRace(context);

            var controller = new EnrolmentsController(context)
            {
                ControllerContext = TestDbFactory.BuildControllerContext(participant.UserID, "Participant")
            };

            await controller.Enrol(new CreateEnrolmentDto { CategoryId = category.CategoryID });
            var secondAttempt = await controller.Enrol(new CreateEnrolmentDto { CategoryId = category.CategoryID });

            Assert.IsType<ConflictObjectResult>(secondAttempt);
            Assert.Single(context.Enrolments);
        }

        [Fact]
        public async Task Organiser_CanUpdateEnrolmentStatus_ForOwnEvent()
        {
            var context = TestDbFactory.CreateContext();
            var (organiser, participant, category) = SeedRace(context);

            var participantController = new EnrolmentsController(context)
            {
                ControllerContext = TestDbFactory.BuildControllerContext(participant.UserID, "Participant")
            };
            await participantController.Enrol(new CreateEnrolmentDto { CategoryId = category.CategoryID });
            var enrolmentId = Assert.Single(context.Enrolments).EnrolmentID;

            var organiserController = new EnrolmentsController(context)
            {
                ControllerContext = TestDbFactory.BuildControllerContext(organiser.UserID, "Organiser")
            };

            var result = await organiserController.UpdateEnrolmentStatus(
                enrolmentId, new UpdateEnrolmentStatusDto { Status = "Cancelled" });

            Assert.IsType<OkObjectResult>(result);
            Assert.Equal(EnrolmentStatus.Cancelled, context.Enrolments.Single().Status);
        }

        [Fact]
        public async Task Organiser_CannotUpdateEnrolmentStatus_ForAnotherOrganisersEvent()
        {
            var context = TestDbFactory.CreateContext();
            var (_, participant, category) = SeedRace(context);

            var participantController = new EnrolmentsController(context)
            {
                ControllerContext = TestDbFactory.BuildControllerContext(participant.UserID, "Participant")
            };
            await participantController.Enrol(new CreateEnrolmentDto { CategoryId = category.CategoryID });
            var enrolmentId = Assert.Single(context.Enrolments).EnrolmentID;

            var otherOrganiser = new User
            {
                FullName = "Another Organiser",
                Email = "other.organiser@raceday.co.za",
                PasswordHash = "irrelevant-for-this-test",
                Role = UserRole.Organiser
            };
            context.Users.Add(otherOrganiser);
            context.SaveChanges();

            var otherOrganiserController = new EnrolmentsController(context)
            {
                ControllerContext = TestDbFactory.BuildControllerContext(otherOrganiser.UserID, "Organiser")
            };

            var result = await otherOrganiserController.UpdateEnrolmentStatus(
                enrolmentId, new UpdateEnrolmentStatusDto { Status = "Cancelled" });

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(403, objectResult.StatusCode);
        }
    }
}
