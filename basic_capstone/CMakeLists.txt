cmake_minimum_required(VERSION 3.8)
project(basic_capstone)

# ============================================================
# 컴파일러 옵션
# ============================================================
if(CMAKE_COMPILER_IS_GNUCXX OR CMAKE_CXX_COMPILER_ID MATCHES "Clang")
  add_compile_options(-Wall -Wextra -Wpedantic)
endif()

# ============================================================
# 의존성 패키지
# ============================================================
find_package(ament_cmake REQUIRED)
find_package(rosidl_default_generators REQUIRED)
find_package(rclcpp REQUIRED)
find_package(std_msgs REQUIRED)

# ============================================================
# 커스텀 메시지 타입
# ============================================================
rosidl_generate_interfaces(${PROJECT_NAME}
        "msg/GraspCommand.msg"
        "msg/GraspFeedback.msg"
)

# ============================================================
# 테스트용 노드 (publisher / subscriber)
# ============================================================
add_executable(publisher src/publisher.cpp)
ament_target_dependencies(publisher rclcpp std_msgs)

add_executable(subscriber src/subscriber.cpp)
ament_target_dependencies(subscriber rclcpp std_msgs)

# ============================================================
# GraspCommand 노드
# ============================================================
rosidl_get_typesupport_target(cpp_typesupport_target ${PROJECT_NAME} "rosidl_typesupport_cpp")

add_executable(grasp_command_publisher src/grasp_command_publisher.cpp)
ament_target_dependencies(grasp_command_publisher rclcpp)
target_link_libraries(grasp_command_publisher ${cpp_typesupport_target})

add_executable(grasp_command_subscriber src/grasp_command_subscriber.cpp)
ament_target_dependencies(grasp_command_subscriber rclcpp)
target_link_libraries(grasp_command_subscriber ${cpp_typesupport_target})

add_executable(state_feedback_node src/state_feedback_node.cpp)
ament_target_dependencies(state_feedback_node rclcpp)
target_link_libraries(state_feedback_node ${cpp_typesupport_target})

# ============================================================
# 실행 파일 설치
# ============================================================
install(TARGETS
        publisher
        subscriber
        grasp_command_publisher
        grasp_command_subscriber
        state_feedback_node
        DESTINATION lib/${PROJECT_NAME}
)

# ============================================================
# 테스트
# ============================================================
if(BUILD_TESTING)
  find_package(ament_lint_auto REQUIRED)
  set(ament_cmake_copyright_FOUND TRUE)
  set(ament_cmake_cpplint_FOUND TRUE)
  ament_lint_auto_find_test_dependencies()
endif()

ament_package()