#include "rclcpp/rclcpp.hpp"
#include "basic_capstone/msg/grasp_command.hpp"

class GraspCommandSubscriber : public rclcpp::Node
{
public:
    GraspCommandSubscriber() : Node("grasp_command_subscriber")
    {
        subscription_ = this->create_subscription<basic_capstone::msg::GraspCommand>(
          "grasp_command", 10,
          std::bind(&GraspCommandSubscriber::topic_callback, this, std::placeholders::_1));
    }

private:
    void topic_callback(const basic_capstone::msg::GraspCommand & msg)
    {
        RCLCPP_INFO(this->get_logger(), "Received - object: '%s', force: %.2f, fragile: %s",
          msg.object_type.c_str(), msg.force, msg.is_fragile ? "true" : "false");
    }

    rclcpp::Subscription<basic_capstone::msg::GraspCommand>::SharedPtr subscription_;
};

int main(int argc, char * argv[])
{
    rclcpp::init(argc, argv);
    rclcpp::spin(std::make_shared<GraspCommandSubscriber>());
    rclcpp::shutdown();
    return 0;
}