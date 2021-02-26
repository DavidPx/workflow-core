using System;
using System.Collections.Generic;
using System.Text;
using WorkflowCore.Interface;
using WorkflowCore.Models;
using Xunit;
using FluentAssertions;
using System.Linq;
using WorkflowCore.Testing;
// ReSharper disable PossibleNullReferenceException

namespace WorkflowCore.IntegrationTests.Scenarios
{
    public class SagaWithBranchAndExceptionScenario : WorkflowTest<SagaWithBranchAndExceptionScenario.Workflow, SagaWithBranchAndExceptionScenario.MyDataClass>
    {
        public class MyDataClass
        {
            public bool ThrowException { get; set; }
            public bool ThrowExceptionInBranchA { get; set; }
            public bool ThrowExceptionInBranchA1 { get; set; }
            public string SomeCondition { get; set; }
            public bool EventA1Fired { get; set; }
            public bool EventBFired { get; set; }

            public bool Event1Fired;
            public bool Event2Fired;
            public bool Event3Fired;
            public bool EventAFired;
            public bool TailEventFired;
            public bool Compensation1Fired;
            public bool Compensation2Fired;
            public bool Compensation3Fired;
            public bool Compensation4Fired;
            public bool SagaCompensationFired;
            public bool TailEventCompensationFired;
        }

        public class Workflow : IWorkflow<MyDataClass>
        {


            public string Id => "SagaWithBranchAndExceptionScenarioWorkflow";
            public int Version => 1;
            public void Build(IWorkflowBuilder<MyDataClass> builder)
            {
                builder
                    .StartWith(context => ExecutionResult.Next())
                        .CompensateWith(context => (context.Workflow.Data as MyDataClass).Compensation1Fired = true)
                    .Saga(x => x
                        .StartWith(context => ExecutionResult.Next())
                            .CompensateWith(context => (context.Workflow.Data as MyDataClass).Compensation2Fired = true)
                        .Then(context =>
                            {
                                (context.Workflow.Data as MyDataClass).Event1Fired = true;
                                if ((context.Workflow.Data as MyDataClass).ThrowException)
                                    throw new Exception();
                                (context.Workflow.Data as MyDataClass).Event2Fired = true;
                            })
                            .CompensateWith(context => (context.Workflow.Data as MyDataClass).Compensation3Fired = true)
                        .Then(context => (context.Workflow.Data as MyDataClass).Event3Fired = true)
                            .CompensateWith(context => (context.Workflow.Data as MyDataClass).Compensation4Fired = true)
                        .Decide(data => data.SomeCondition)
                        .Branch( // Branch A
                            "Yep",
                            builder
                                .CreateBranch()
                                .StartWith(context => ExecutionResult.Next())
                                .Then(context =>
                                {
                                    if ((context.Workflow.Data as MyDataClass).ThrowExceptionInBranchA)
                                        throw new Exception();
                                })
                                .Then(context =>
                                {
                                    (context.Workflow.Data as MyDataClass).EventAFired = true;
                                })
                                .Decide(data => data.SomeCondition)
                                .Branch( // Branch A.1
                                    "Yep",
                                    builder
                                        .CreateBranch()
                                        .StartWith(context => ExecutionResult.Next())
                                        .Then(context =>
                                        {
                                            if ((context.Workflow.Data as MyDataClass).ThrowExceptionInBranchA1)
                                                throw new Exception();
                                        })
                                        .Then(context =>
                                        {
                                            (context.Workflow.Data as MyDataClass).EventA1Fired = true;
                                        })
                                    )
                            )
                        .Decide(data => data.SomeCondition)
                        .Branch( // Branch B
                            "Yep",
                            builder
                                .CreateBranch()
                                .StartWith(context => ExecutionResult.Next())
                                .Then(context =>
                                {
                                    (context.Workflow.Data as MyDataClass).EventBFired = true;
                                })
                            )
                        )
                        .CompensateWith(context => (context.Workflow.Data as MyDataClass).SagaCompensationFired = true)
                    .Then(context => (context.Workflow.Data as MyDataClass).TailEventFired = true)
                        .CompensateWith(context => (context.Workflow.Data as MyDataClass).TailEventCompensationFired = true);
            }
        }

        public SagaWithBranchAndExceptionScenario()
        {
            Setup();
        }

        [Fact]
        public void NoExceptionScenario()
        {
            var data = new MyDataClass() { ThrowException = false, ThrowExceptionInBranchA = false, SomeCondition = "Yep" };

            var workflowId = StartWorkflow(data);
            WaitForWorkflowToComplete(workflowId, TimeSpan.FromSeconds(30));

            GetStatus(workflowId).Should().Be(WorkflowStatus.Complete);
            UnhandledStepErrors.Count.Should().Be(0);
            data.Event1Fired.Should().BeTrue();
            data.Event2Fired.Should().BeTrue();
            data.Event3Fired.Should().BeTrue();
            data.Compensation1Fired.Should().BeFalse();
            data.Compensation2Fired.Should().BeFalse();
            data.Compensation3Fired.Should().BeFalse();
            data.Compensation4Fired.Should().BeFalse();
            data.SagaCompensationFired.Should().BeFalse();
            data.TailEventCompensationFired.Should().BeFalse();
            data.TailEventFired.Should().BeTrue();
            data.EventAFired.Should().BeTrue();
            data.EventA1Fired.Should().BeTrue();
            data.EventBFired.Should().BeTrue();
        }

        [Fact]
        public void ExceptionScenario()
        {
            var data = new MyDataClass() { ThrowException = true, ThrowExceptionInBranchA = false, SomeCondition = "Yep" };

            var workflowId = StartWorkflow(data);
            WaitForWorkflowToComplete(workflowId, TimeSpan.FromSeconds(30));

            GetStatus(workflowId).Should().Be(WorkflowStatus.Complete);
            UnhandledStepErrors.Count.Should().Be(1);
            data.Event1Fired.Should().BeTrue();
            data.Event2Fired.Should().BeFalse();
            data.Event3Fired.Should().BeFalse();
            data.Compensation1Fired.Should().BeFalse();
            data.Compensation2Fired.Should().BeTrue();
            data.Compensation3Fired.Should().BeTrue();
            data.Compensation4Fired.Should().BeFalse();
            data.SagaCompensationFired.Should().BeTrue();
            data.TailEventCompensationFired.Should().BeFalse();
            data.TailEventFired.Should().BeTrue();
            data.EventAFired.Should().BeFalse();
            data.EventA1Fired.Should().BeFalse();
            data.EventBFired.Should().BeFalse();
        }

        [Fact]
        public void ExceptionInBranchScenario()
        {
            var data = new MyDataClass() { ThrowException = false, ThrowExceptionInBranchA = true, SomeCondition = "Yep" };
            var workflowId = StartWorkflow(data);
            WaitForWorkflowToComplete(workflowId, TimeSpan.FromSeconds(30));

            GetStatus(workflowId).Should().Be(WorkflowStatus.Complete);
            UnhandledStepErrors.Count.Should().Be(1);
            data.Event1Fired.Should().BeTrue();
            data.Event2Fired.Should().BeTrue();
            data.Event3Fired.Should().BeTrue();
            data.Compensation1Fired.Should().BeFalse();
            data.Compensation2Fired.Should().BeTrue();
            data.Compensation3Fired.Should().BeTrue();
            data.Compensation4Fired.Should().BeTrue();
            data.SagaCompensationFired.Should().BeTrue();
            data.TailEventCompensationFired.Should().BeFalse();
            data.TailEventFired.Should().BeTrue();
            data.EventAFired.Should().BeFalse();
            data.EventA1Fired.Should().BeFalse();
            
            data.EventBFired.Should().BeFalse(); // The exception in the preceding branch (A) should prevent Branch B from running
        }

        [Fact]
        public void ExceptionInInnerBranchScenario()
        {
            var data = new MyDataClass { ThrowException = false, ThrowExceptionInBranchA = false, ThrowExceptionInBranchA1 = true, SomeCondition = "Yep" };
            var workflowId = StartWorkflow(data);
            WaitForWorkflowToComplete(workflowId, TimeSpan.FromSeconds(30));

            GetStatus(workflowId).Should().Be(WorkflowStatus.Complete);
            UnhandledStepErrors.Count.Should().Be(1);
            data.Event1Fired.Should().BeTrue();
            data.Event2Fired.Should().BeTrue();
            data.Event3Fired.Should().BeTrue();
            data.Compensation1Fired.Should().BeFalse();
            data.Compensation2Fired.Should().BeTrue();
            data.Compensation3Fired.Should().BeTrue();
            data.Compensation4Fired.Should().BeTrue();
            data.SagaCompensationFired.Should().BeTrue();
            data.TailEventCompensationFired.Should().BeFalse();
            data.TailEventFired.Should().BeTrue();
            data.EventAFired.Should().BeTrue();
            data.EventA1Fired.Should().BeFalse();
            
            data.EventBFired.Should().BeFalse(); // The exception in the preceding branch (A) should prevent Branch B from running
        }
    }
}
