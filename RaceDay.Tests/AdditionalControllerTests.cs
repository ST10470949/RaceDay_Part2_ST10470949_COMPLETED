using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using RaceDay.API.Controllers;
using RaceDay.API.Data;
using RaceDay.API.DTOs.Categories;
using RaceDay.API.DTOs.Enrolments;
using RaceDay.API.DTOs.Events;
using RaceDay.API.DTOs.Results;
using RaceDay.API.DTOs.Users;
using RaceDay.API.DTOs.Weather;
using RaceDay.API.Models;
using RaceDay.API.Services;
using RaceDay.Tests.TestHelpers;
using Xunit;

namespace RaceDay.Tests
{
    public class AdditionalControllerTests
    {
        private static User AddUser(RaceDayContext context, string name, string email, UserRole role)
        {
            var user = new User
            {
                FullName = name,
                Email = email,
                PasswordHash = "test-hash",
                Role = role
            };
            context.Users.Add(user);
            context.SaveChanges();
            return user;
        }

        private static (User organiser, User participant, Event raceEvent, Category category) SeedRace(RaceDayContext context)
        {
            var organiser = AddUser(context, "Organiser One", "org1@raceday.test", UserRole.Organiser);
            var participant = AddUser(context, "Participant One", "part1@raceday.test", UserRole.Participant);

            var raceEvent = new Event
            {
                OrganiserID = organiser.UserID,
                Name = "Test Marathon",
                EventDate = DateTime.Today.AddDays(30),
                Location = "Gqeberha",
                Description = "Test race",
                DistanceKm = 42.2m,
                EventType = "Run"
            };
            context.Events.Add(raceEvent);
            context.SaveChanges();

            var category = new Category
            {
                EventID = raceEvent.EventID,
                Name = "42.2km Marathon",
                EntryFee = 350,
                MaxParticipants = 2
            };
            context.Categories.Add(category);
            context.SaveChanges();

            return (organiser, participant, raceEvent, category);
        }

        [Fact]
        public void Logout_ClearsTheSession()
        {
            var context = TestDbFactory.CreateContext();
            var controller = new AuthController(context, new PasswordHashService())
            {
                ControllerContext = TestDbFactory.BuildControllerContext(7, "Participant")
            };

            var result = controller.Logout();

            Assert.IsType<OkObjectResult>(result);
            Assert.Null(controller.ControllerContext.HttpContext.Session.GetInt32("UserId"));
            Assert.Null(controller.ControllerContext.HttpContext.Session.GetString("Role"));
        }

        [Fact]
        public async Task UnauthenticatedProfile_ReturnsUnauthorized()
        {
            var context = TestDbFactory.CreateContext();
            var controller = new UsersController(context)
            {
                ControllerContext = TestDbFactory.BuildControllerContext()
            };

            var result = await controller.GetMyProfile();

            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task UserCanUpdateOnlyTheirOwnProfile()
        {
            var context = TestDbFactory.CreateContext();
            var user = AddUser(context, "Old Name", "old@raceday.test", UserRole.Participant);

            var controller = new UsersController(context)
            {
                ControllerContext = TestDbFactory.BuildControllerContext(user.UserID, "Participant")
            };

            var result = await controller.UpdateMyProfile(new UpdateProfileDto
            {
                FullName = "New Name",
                Email = "new@raceday.test"
            });

            Assert.IsType<OkObjectResult>(result);
            var saved = await context.Users.FindAsync(user.UserID);
            Assert.Equal("New Name", saved!.FullName);
            Assert.Equal("new@raceday.test", saved.Email);
        }

        [Fact]
        public async Task DuplicateProfileEmail_IsRejected()
        {
            var context = TestDbFactory.CreateContext();
            var first = AddUser(context, "First", "first@raceday.test", UserRole.Participant);
            AddUser(context, "Second", "second@raceday.test", UserRole.Participant);

            var controller = new UsersController(context)
            {
                ControllerContext = TestDbFactory.BuildControllerContext(first.UserID, "Participant")
            };

            var result = await controller.UpdateMyProfile(new UpdateProfileDto
            {
                FullName = "First",
                Email = "second@raceday.test"
            });

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, bad.StatusCode);
        }

        [Fact]
        public async Task EventOwnerCanUpdateTheirEvent()
        {
            var context = TestDbFactory.CreateContext();
            var (organiser, _, raceEvent, _) = SeedRace(context);
            var controller = new EventsController(context)
            {
                ControllerContext = TestDbFactory.BuildControllerContext(organiser.UserID, "Organiser")
            };

            var result = await controller.UpdateEvent(raceEvent.EventID, new UpdateEventDto
            {
                Name = "Updated Marathon",
                EventDate = raceEvent.EventDate,
                Location = "East London",
                Description = "Updated",
                DistanceKm = 21.1m,
                EventType = "Run"
            });

            Assert.IsType<OkObjectResult>(result);
            Assert.Equal("Updated Marathon", context.Events.Single().Name);
        }

        [Fact]
        public async Task DifferentOrganiserCannotUpdateEvent()
        {
            var context = TestDbFactory.CreateContext();
            var (_, _, raceEvent, _) = SeedRace(context);
            var other = AddUser(context, "Other", "other@raceday.test", UserRole.Organiser);

            var controller = new EventsController(context)
            {
                ControllerContext = TestDbFactory.BuildControllerContext(other.UserID, "Organiser")
            };

            var result = await controller.UpdateEvent(raceEvent.EventID, new UpdateEventDto
            {
                Name = "Should Fail",
                EventDate = raceEvent.EventDate,
                Location = "Durban",
                DistanceKm = 10,
                EventType = "Run"
            });

            var forbidden = Assert.IsType<ObjectResult>(result);
            Assert.Equal(403, forbidden.StatusCode);
        }

        [Fact]
        public async Task EventOwnerCanDeleteTheirEvent()
        {
            var context = TestDbFactory.CreateContext();
            var (organiser, _, raceEvent, _) = SeedRace(context);
            var controller = new EventsController(context)
            {
                ControllerContext = TestDbFactory.BuildControllerContext(organiser.UserID, "Organiser")
            };

            var result = await controller.DeleteEvent(raceEvent.EventID);

            Assert.IsType<NoContentResult>(result);
            Assert.Empty(context.Events);
        }

        [Fact]
        public async Task CategoryOwnerCanUpdateCategory()
        {
            var context = TestDbFactory.CreateContext();
            var (organiser, _, _, category) = SeedRace(context);
            var controller = new CategoriesController(context)
            {
                ControllerContext = TestDbFactory.BuildControllerContext(organiser.UserID, "Organiser")
            };

            var result = await controller.UpdateCategory(category.CategoryID, new CreateCategoryDto
            {
                Name = "Updated Category",
                EntryFee = 500,
                MaxParticipants = 10
            });

            Assert.IsType<OkObjectResult>(result);
            Assert.Equal("Updated Category", context.Categories.Single().Name);
        }

        [Fact]
        public async Task DifferentOrganiserCannotDeleteCategory()
        {
            var context = TestDbFactory.CreateContext();
            var (_, _, _, category) = SeedRace(context);
            var other = AddUser(context, "Other", "other2@raceday.test", UserRole.Organiser);

            var controller = new CategoriesController(context)
            {
                ControllerContext = TestDbFactory.BuildControllerContext(other.UserID, "Organiser")
            };

            var result = await controller.DeleteCategory(category.CategoryID);

            var forbidden = Assert.IsType<ObjectResult>(result);
            Assert.Equal(403, forbidden.StatusCode);
            Assert.Single(context.Categories);
        }

        [Fact]
        public async Task ParticipantCanViewTheirOwnEnrolments()
        {
            var context = TestDbFactory.CreateContext();
            var (_, participant, _, category) = SeedRace(context);

            context.Enrolments.Add(new Enrolment
            {
                ParticipantID = participant.UserID,
                CategoryID = category.CategoryID,
                Status = EnrolmentStatus.Confirmed
            });
            context.SaveChanges();

            var controller = new EnrolmentsController(context)
            {
                ControllerContext = TestDbFactory.BuildControllerContext(participant.UserID, "Participant")
            };

            var result = await controller.GetMyEnrolments();

            var ok = Assert.IsType<OkObjectResult>(result);
            var items = Assert.IsAssignableFrom<System.Collections.IEnumerable>(ok.Value).Cast<object>().ToList();
            Assert.Single(items);
        }

        [Fact]
        public async Task FullCategoryRejectsNewParticipant()
        {
            var context = TestDbFactory.CreateContext();
            var (_, participant, _, category) = SeedRace(context);
            category.MaxParticipants = 1;
            context.SaveChanges();

            var other = AddUser(context, "Other Participant", "otherpart@raceday.test", UserRole.Participant);
            context.Enrolments.Add(new Enrolment
            {
                ParticipantID = other.UserID,
                CategoryID = category.CategoryID
            });
            context.SaveChanges();

            var controller = new EnrolmentsController(context)
            {
                ControllerContext = TestDbFactory.BuildControllerContext(participant.UserID, "Participant")
            };

            var result = await controller.Enrol(new CreateEnrolmentDto { CategoryId = category.CategoryID });

            Assert.IsType<ConflictObjectResult>(result);
        }

        [Fact]
        public async Task ParticipantCanCancelTheirOwnEnrolment()
        {
            var context = TestDbFactory.CreateContext();
            var (_, participant, _, category) = SeedRace(context);
            var enrolment = new Enrolment
            {
                ParticipantID = participant.UserID,
                CategoryID = category.CategoryID
            };
            context.Enrolments.Add(enrolment);
            context.SaveChanges();

            var controller = new EnrolmentsController(context)
            {
                ControllerContext = TestDbFactory.BuildControllerContext(participant.UserID, "Participant")
            };

            var result = await controller.CancelEnrolment(enrolment.EnrolmentID);

            Assert.IsType<NoContentResult>(result);
            Assert.Empty(context.Enrolments);
        }

        [Fact]
        public async Task OrganiserCanCaptureResultForOwnEvent()
        {
            var context = TestDbFactory.CreateContext();
            var (organiser, participant, _, category) = SeedRace(context);
            var enrolment = new Enrolment
            {
                ParticipantID = participant.UserID,
                CategoryID = category.CategoryID
            };
            context.Enrolments.Add(enrolment);
            context.SaveChanges();

            var controller = new ResultsController(context)
            {
                ControllerContext = TestDbFactory.BuildControllerContext(organiser.UserID, "Organiser")
            };

            var result = await controller.CaptureResult(new CreateResultDto
            {
                EnrolmentId = enrolment.EnrolmentID,
                FinishTime = new TimeSpan(3, 40, 12),
                Position = 15
            });

            Assert.Equal(201, Assert.IsType<ObjectResult>(result).StatusCode);
            Assert.Single(context.Results);
        }

        [Fact]
        public async Task ParticipantCannotCaptureResult()
        {
            var context = TestDbFactory.CreateContext();
            var (_, participant, _, category) = SeedRace(context);
            var enrolment = new Enrolment
            {
                ParticipantID = participant.UserID,
                CategoryID = category.CategoryID
            };
            context.Enrolments.Add(enrolment);
            context.SaveChanges();

            var controller = new ResultsController(context)
            {
                ControllerContext = TestDbFactory.BuildControllerContext(participant.UserID, "Participant")
            };

            var result = await controller.CaptureResult(new CreateResultDto
            {
                EnrolmentId = enrolment.EnrolmentID,
                Position = 1
            });

            var forbidden = Assert.IsType<ObjectResult>(result);
            Assert.Equal(403, forbidden.StatusCode);
        }

        [Fact]
        public async Task ParticipantOnlySeesTheirOwnResults()
        {
            var context = TestDbFactory.CreateContext();
            var (organiser, participant, raceEvent, category) = SeedRace(context);
            var otherParticipant = AddUser(context, "Other Participant", "other3@raceday.test", UserRole.Participant);

            var firstEnrolment = new Enrolment { ParticipantID = participant.UserID, CategoryID = category.CategoryID };
            context.Enrolments.Add(firstEnrolment);
            context.SaveChanges();

            var secondCategory = new Category { EventID = raceEvent.EventID, Name = "10km", EntryFee = 100, MaxParticipants = 10 };
            context.Categories.Add(secondCategory);
            context.SaveChanges();

            var secondEnrolment = new Enrolment { ParticipantID = otherParticipant.UserID, CategoryID = secondCategory.CategoryID };
            context.Enrolments.Add(secondEnrolment);
            context.SaveChanges();

            context.Results.AddRange(
                new Result { EnrolmentID = firstEnrolment.EnrolmentID, CapturedByID = organiser.UserID, Position = 1 },
                new Result { EnrolmentID = secondEnrolment.EnrolmentID, CapturedByID = organiser.UserID, Position = 2 });
            context.SaveChanges();

            var controller = new ResultsController(context)
            {
                ControllerContext = TestDbFactory.BuildControllerContext(participant.UserID, "Participant")
            };

            var result = await controller.GetMyResults();

            var ok = Assert.IsType<OkObjectResult>(result);
            var items = Assert.IsAssignableFrom<System.Collections.IEnumerable>(ok.Value).Cast<object>().ToList();
            Assert.Single(items);
        }

        [Fact]
        public async Task EventOwnerCanCreateAndUpdateWeatherInfo()
        {
            var context = TestDbFactory.CreateContext();
            var (organiser, _, raceEvent, _) = SeedRace(context);
            var controller = new EventsController(context)
            {
                ControllerContext = TestDbFactory.BuildControllerContext(organiser.UserID, "Organiser")
            };

            var created = await controller.SetWeather(raceEvent.EventID, new CreateWeatherDto
            {
                WeatherForecast = "Sunny",
                RouteMapUrl = "https://example.com/route"
            });

            Assert.Equal(201, Assert.IsType<ObjectResult>(created).StatusCode);
            Assert.Single(context.RouteWeatherInfos);

            var updated = await controller.SetWeather(raceEvent.EventID, new CreateWeatherDto
            {
                WeatherForecast = "Cloudy",
                RouteMapUrl = "https://example.com/route2"
            });

            Assert.IsType<OkObjectResult>(updated);
            Assert.Equal("Cloudy", context.RouteWeatherInfos.Single().WeatherForecast);
        }
    }
}
