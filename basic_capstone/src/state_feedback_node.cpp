#include "rclcpp/rclcpp.hpp"
#include "basic_capstone/msg/grasp_command.hpp"
#include "basic_capstone/msg/grasp_feedback.hpp"

class StateFeedbackNode : public rclcpp::Node
{
public:
    StateFeedbackNode() : Node("state_feedback_node")
    {
        subscription_ = this->create_subscription<basic_capstone::msg::GraspCommand>(
            "grasp_command", 10,
            std::bind(&StateFeedbackNode::command_callback, this, std::placeholders::_1));

        publisher_ = this->create_publisher<basic_capstone::msg::GraspFeedback>("grasp_feedback", 10);

        RCLCPP_INFO(this->get_logger(), "StateFeedbackNode 시작됨");
    }

private:
    void command_callback(const basic_capstone::msg::GraspCommand& cmd)
    {
        RCLCPP_INFO(this->get_logger(),
            "명령 수신 - object: '%s', force: %.2f, fragile: %s, move_speed: %.2f, motion_style: '%s'",
            cmd.object_type.c_str(), cmd.force,
            cmd.is_fragile ? "true" : "false",
            cmd.move_speed, cmd.motion_style.c_str());

        auto feedback = basic_capstone::msg::GraspFeedback();
        feedback.actual_force = cmd.force * 0.95f;

        // motion_style 기반 성공 여부 판단
        if (cmd.is_fragile && cmd.force > 3.0) {
            feedback.success = false;
            feedback.status = "object_damaged";
        }
        else if (cmd.force < 0.5) {
            feedback.success = false;
            feedback.status = "object_dropped";
        }
        else {
            feedback.success = true;
            feedback.status = "success:" + cmd.motion_style + ":" + std::to_string(cmd.move_speed);
        }

        RCLCPP_INFO(this->get_logger(),
            "피드백 발행 - success: %s, actual_force: %.2f, status: '%s'",
            feedback.success ? "true" : "false",
            feedback.actual_force, feedback.status.c_str());

        publisher_->publish(feedback);
    }

    rclcpp::Subscription<basic_capstone::msg::GraspCommand>::SharedPtr subscription_;
    rclcpp::Publisher<basic_capstone::msg::GraspFeedback>::SharedPtr publisher_;
};

int main(int argc, char* argv[])
{
    rclcpp::init(argc, argv);
    rclcpp::spin(std::make_shared<StateFeedbackNode>());
    rclcpp::shutdown();
    return 0;
}