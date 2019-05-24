﻿using JsonApiDotNetCore.Hooks;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCoreExample.Data;
using JsonApiDotNetCoreExample.Models;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace UnitTests.ResourceHooks
{
    public class BeforeUpdate_WithDbValues_Tests : HooksTestsSetup
    {
        private readonly ResourceHook[] targetHooks = { ResourceHook.BeforeUpdate, ResourceHook.BeforeImplicitUpdateRelationship, ResourceHook.BeforeUpdateRelationship };
        private readonly ResourceHook[] targetHooksNoImplicit = { ResourceHook.BeforeUpdate, ResourceHook.BeforeUpdateRelationship };

        private readonly string description = "DESCRIPTION";
        private readonly string lastName = "NAME";
        private readonly string personId;
        private readonly List<TodoItem> todoList;
        private readonly DbContextOptions<AppDbContext> options;

        public BeforeUpdate_WithDbValues_Tests()
        {
            todoList = CreateTodoWithToOnePerson();

            var todoId = todoList[0].Id;
            var _personId = todoList[0].ToOnePerson.Id;
            personId = _personId.ToString();
            var _implicitPersonId = (_personId + 10000);

            var implicitTodo = _todoFaker.Generate();
            implicitTodo.Id += 1000;
            implicitTodo.ToOnePersonId = _personId;
            implicitTodo.Description = description + description;

            options = InitInMemoryDb(context =>
            {
                context.Set<Person>().Add(new Person { Id = _personId, LastName = lastName });
                context.Set<Person>().Add(new Person { Id = _implicitPersonId, LastName = lastName + lastName });
                context.Set<TodoItem>().Add(new TodoItem { Id = todoId, ToOnePersonId = _implicitPersonId, Description = description });
                context.Set<TodoItem>().Add(implicitTodo);
                context.SaveChanges();
            });
        }

        [Fact]
        public void BeforeUpdate()
        {
            // arrange
            var todoDiscovery = SetDiscoverableHooks<TodoItem>(targetHooks, EnableDbValues);
            var personDiscovery = SetDiscoverableHooks<Person>(targetHooks, EnableDbValues);
            (var contextMock, var hookExecutor, var todoResourceMock,
                var ownerResourceMock) = CreateTestObjects(todoDiscovery, personDiscovery, repoDbContextOptions: options);

            // act
            hookExecutor.BeforeUpdate(todoList, ResourcePipeline.Patch);

            // assert
            todoResourceMock.Verify(rd => rd.BeforeUpdate(It.Is<EntityDiff<TodoItem>>((diff) => TodoCheck(diff, description)), ResourcePipeline.Patch), Times.Once());
            ownerResourceMock.Verify(rd => rd.BeforeUpdateRelationship(
                It.Is<HashSet<string>>(ids => PersonIdCheck(ids, personId)),
                It.Is<IAffectedRelationships<Person>>(rh => PersonCheck(lastName, rh)),
                ResourcePipeline.Patch),
                Times.Once());
            ownerResourceMock.Verify(rd => rd.BeforeImplicitUpdateRelationship(
                It.Is<IAffectedRelationships<Person>>(rh => PersonCheck(lastName + lastName, rh)),
                ResourcePipeline.Patch),
                Times.Once());
            todoResourceMock.Verify(rd => rd.BeforeImplicitUpdateRelationship(
                It.Is<IAffectedRelationships<TodoItem>>( rh => TodoCheck(rh, description + description)),
                ResourcePipeline.Patch),
                Times.Once());
            VerifyNoOtherCalls(todoResourceMock, ownerResourceMock);
        }


        [Fact]
        public void BeforeUpdate_Deleting_Relationship()
        {
            // arrange
            var todoDiscovery = SetDiscoverableHooks<TodoItem>(targetHooks, EnableDbValues);
            var personDiscovery = SetDiscoverableHooks<Person>(targetHooks, EnableDbValues);
            (var contextMock, var hookExecutor, var todoResourceMock,
                var ownerResourceMock) = CreateTestObjects(todoDiscovery, personDiscovery, repoDbContextOptions: options);
            var attr = ResourceGraph.Instance.GetContextEntity(typeof(TodoItem)).Relationships.Single(r => r.PublicRelationshipName == "one-to-one-person");
            contextMock.Setup(c => c.RelationshipsToUpdate).Returns(new Dictionary<RelationshipAttribute, object>() { { attr, new object() } });

            // act
            var _todoList = new List<TodoItem>() { new TodoItem { Id = this.todoList[0].Id } };
            hookExecutor.BeforeUpdate(_todoList, ResourcePipeline.Patch);

            // assert
            todoResourceMock.Verify(rd => rd.BeforeUpdate(It.Is<EntityDiff<TodoItem>>((diff) => TodoCheck(diff, description)), ResourcePipeline.Patch), Times.Once());
            ownerResourceMock.Verify(rd => rd.BeforeImplicitUpdateRelationship(
                It.Is<IAffectedRelationships<Person>>(rh => PersonCheck(lastName + lastName, rh)),
                ResourcePipeline.Patch),
                Times.Once());
            VerifyNoOtherCalls(todoResourceMock, ownerResourceMock);
        }


        [Fact]
        public void BeforeUpdate_Without_Parent_Hook_Implemented()
        {
            // arrange
            var todoDiscovery = SetDiscoverableHooks<TodoItem>(NoHooks, DisableDbValues);
            var personDiscovery = SetDiscoverableHooks<Person>(targetHooks, EnableDbValues);
            (var contextMock, var hookExecutor, var todoResourceMock,
                var ownerResourceMock) = CreateTestObjects(todoDiscovery, personDiscovery, repoDbContextOptions: options);

            // act
            hookExecutor.BeforeUpdate(todoList, ResourcePipeline.Patch);

            // assert
            ownerResourceMock.Verify(rd => rd.BeforeUpdateRelationship(
                It.Is<HashSet<string>>(ids => PersonIdCheck(ids, personId)),
                It.Is<IAffectedRelationships<Person>>(rh => PersonCheck(lastName, rh)),
                ResourcePipeline.Patch),
                Times.Once());
            ownerResourceMock.Verify(rd => rd.BeforeImplicitUpdateRelationship(
                It.Is<IAffectedRelationships<Person>>(rh => PersonCheck(lastName + lastName, rh)),
                ResourcePipeline.Patch),
                Times.Once());
            VerifyNoOtherCalls(todoResourceMock, ownerResourceMock);
        }

        [Fact]
        public void BeforeUpdate_Without_Child_Hook_Implemented()
        {
            // arrange
            var todoDiscovery = SetDiscoverableHooks<TodoItem>(targetHooks, EnableDbValues);
            var personDiscovery = SetDiscoverableHooks<Person>(NoHooks, DisableDbValues);
            (var contextMock, var hookExecutor, var todoResourceMock,
                var ownerResourceMock) = CreateTestObjects(todoDiscovery, personDiscovery, repoDbContextOptions: options);

            // act
            hookExecutor.BeforeUpdate(todoList, ResourcePipeline.Patch);

            // assert
            todoResourceMock.Verify(rd => rd.BeforeUpdate(It.Is<EntityDiff<TodoItem>>((diff) => TodoCheck(diff, description)), ResourcePipeline.Patch), Times.Once());
            todoResourceMock.Verify(rd => rd.BeforeImplicitUpdateRelationship(
                It.Is<IAffectedRelationships<TodoItem>>(rh => TodoCheck(rh, description + description)),
                ResourcePipeline.Patch),
                Times.Once());
            VerifyNoOtherCalls(todoResourceMock, ownerResourceMock);
        }

        [Fact]
        public void BeforeUpdate_NoImplicit()
        {
            // arrange
            var todoDiscovery = SetDiscoverableHooks<TodoItem>(targetHooksNoImplicit, ResourceHook.BeforeUpdate );
            var personDiscovery = SetDiscoverableHooks<Person>(targetHooksNoImplicit, ResourceHook.BeforeUpdateRelationship );
            (var contextMock, var hookExecutor, var todoResourceMock,
                var ownerResourceMock) = CreateTestObjects(todoDiscovery, personDiscovery, repoDbContextOptions: options);

            // act
            hookExecutor.BeforeUpdate(todoList, ResourcePipeline.Patch);

            // assert
            todoResourceMock.Verify(rd => rd.BeforeUpdate(It.Is<EntityDiff<TodoItem>>((diff) => TodoCheck(diff, description)), ResourcePipeline.Patch), Times.Once());
            ownerResourceMock.Verify(rd => rd.BeforeUpdateRelationship(
                It.Is<HashSet<string>>(ids => PersonIdCheck(ids, personId)),
                It.IsAny<IAffectedRelationships<Person>>(),
                ResourcePipeline.Patch),
                Times.Once());
            VerifyNoOtherCalls(todoResourceMock, ownerResourceMock);
        }

        [Fact]
        public void BeforeUpdate_NoImplicit_Without_Parent_Hook_Implemented()
        {
            // arrange
            var todoDiscovery = SetDiscoverableHooks<TodoItem>(NoHooks, DisableDbValues);
            var personDiscovery = SetDiscoverableHooks<Person>(targetHooksNoImplicit, ResourceHook.BeforeUpdateRelationship );
            (var contextMock, var hookExecutor, var todoResourceMock,
                var ownerResourceMock) = CreateTestObjects(todoDiscovery, personDiscovery, repoDbContextOptions: options);

            // act
            hookExecutor.BeforeUpdate(todoList, ResourcePipeline.Patch);

            // assert
            ownerResourceMock.Verify(rd => rd.BeforeUpdateRelationship(
                It.Is<HashSet<string>>(ids => PersonIdCheck(ids, personId)),
                It.Is<IAffectedRelationships<Person>>(rh => PersonCheck(lastName, rh)),
                ResourcePipeline.Patch),
                Times.Once());
            VerifyNoOtherCalls(todoResourceMock, ownerResourceMock);
        }

        [Fact]
        public void BeforeUpdate_NoImplicit_Without_Child_Hook_Implemented()
        {
            // arrange
            var todoDiscovery = SetDiscoverableHooks<TodoItem>(targetHooksNoImplicit, ResourceHook.BeforeUpdate);
            var personDiscovery = SetDiscoverableHooks<Person>(NoHooks, DisableDbValues);
            (var contextMock, var hookExecutor, var todoResourceMock,
                var ownerResourceMock) = CreateTestObjects(todoDiscovery, personDiscovery, repoDbContextOptions: options);

            // act
            hookExecutor.BeforeUpdate(todoList, ResourcePipeline.Patch);

            // assert
            todoResourceMock.Verify(rd => rd.BeforeUpdate(It.Is<EntityDiff<TodoItem>>((diff) => TodoCheck(diff, description)), ResourcePipeline.Patch), Times.Once());
            VerifyNoOtherCalls(todoResourceMock, ownerResourceMock);
        }

        private bool TodoCheck(EntityDiff<TodoItem> diff, string checksum)
        {
            var dbCheck = diff.DatabaseEntities.Single().Description == checksum;
            var reqCheck = diff.RequestEntities.Single().Description == null;
            return (dbCheck && reqCheck);
        }

        private bool TodoCheck(IAffectedRelationships<TodoItem> rh, string checksum)
        {
            return rh.GetByRelationship<Person>().Single().Value.First().Description == checksum;
        }

        private bool PersonIdCheck(IEnumerable<string> ids, string checksum)
        {
            return ids.Single() == checksum;
        }

        private bool PersonCheck(string checksum, IAffectedRelationships<Person> helper)
        {
            var entries = helper.GetByRelationship<TodoItem>();
            return entries.Single().Value.Single().LastName == checksum;
        }
    }
}

