#include "rclcpp/rclcpp.hpp"
#include "basic_capstone/msg/grasp_command.hpp"

class GraspCommandPublisher : public rclcpp::Node
{
public:
    GraspCommandPublisher() : Node("grasp_command_publisher")
    {
        publisher_ = this->create_publisher<basic_capstone::msg::GraspCommand>("grasp_command", 10);
        timer_ = this->create_wall_timer(
          std::chrono::milliseconds(1000),
          std::bind(&GraspCommandPublisher::timer_callback, this));
    }

private:
    void timer_callback()
    {
        auto message = basic_capstone::msg::GraspCommand();
        message.force = 2.5;
        message.object_type = "egg";
        message.is_fragile = true;

        RCLCPP_INFO(this->get_logger(), "Publishing - object: '%s', force: %.2f, fragile: %s",
          message.object_type.c_str(), message.force, message.is_fragile ? "true" : "false");

        publisher_->publish(message);
    }

    rclcpp::Publisher<basic_capstone::msg::GraspCommand>::SharedPtr publisher_;
    rclcpp::TimerBase::SharedPtr timer_;
};

int main(int argc, char * argv[])
{
    rclcpp::init(argc, argv);
    rclcpp::spin(std::make_shared<GraspCommandPublisher>());
    rclcpp::shutdown();
    return 0;
}