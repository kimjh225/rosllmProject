#include "rclcpp/rclcpp.hpp"
#include "basic_capstone/msg/grasp_command.hpp"
#include "basic_capstone/msg/grasp_feedback.hpp"

class StateFeedbackNode : public rclcpp::Node
{
public:
  StateFeedbackNode() : Node("state_feedback_node")
  {
    // GraspCommand 구독 (LLM으로부터 명령 수신)
    subscription_ = this->create_subscription<basic_capstone::msg::GraspCommand>(
      "grasp_command", 10,
      std::bind(&StateFeedbackNode::command_callback, this, std::placeholders::_1));

    // GraspFeedback 발행 (LLM으로 결과 전송)
    publisher_ = this->create_publisher<basic_capstone::msg::GraspFeedback>("grasp_feedback", 10);

    RCLCPP_INFO(this->get_logger(), "StateFeedbackNode 시작됨");
  }

private:
  void command_callback(const basic_capstone::msg::GraspCommand & cmd)
  {
    RCLCPP_INFO(this->get_logger(), "명령 수신 - object: '%s', force: %.2f, fragile: %s",
      cmd.object_type.c_str(), cmd.force, cmd.is_fragile ? "true" : "false");

    // 더미 파지 시뮬레이션
    auto feedback = basic_capstone::msg::GraspFeedback();
    feedback.actual_force = cmd.force * 0.95;  // 실제 힘은 목표치의 95%로 가정

    // 파지 성공 여부 판단 (더미 로직)
    if (cmd.is_fragile && cmd.force > 5.0) {
      // 약한 물체에 너무 강한 힘 → 실패
      feedback.success = false;
      feedback.status = "object_damaged";
    } else if (cmd.force < 0.5) {
      // 너무 약한 힘 → 실패
      feedback.success = false;
      feedback.status = "object_dropped";
    } else {
      // 성공
      feedback.success = true;
      feedback.status = "success";
    }

    RCLCPP_INFO(this->get_logger(), "피드백 발행 - success: %s, actual_force: %.2f, status: '%s'",
      feedback.success ? "true" : "false", feedback.actual_force, feedback.status.c_str());

    publisher_->publish(feedback);
  }

  rclcpp::Subscription<basic_capstone::msg::GraspCommand>::SharedPtr subscription_;
  rclcpp::Publisher<basic_capstone::msg::GraspFeedback>::SharedPtr publisher_;
};

int main(int argc, char * argv[])
{
  rclcpp::init(argc, argv);
  rclcpp::spin(std::make_shared<StateFeedbackNode>());
  rclcpp::shutdown();
  return 0;
}