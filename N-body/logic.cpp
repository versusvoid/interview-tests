#include "logic.h"

#include <random>
#include <cassert>
#include <iostream>
#include <chrono>


const double Particle::Radius = 7;

const double Logic::dt = 0.03;


Logic::Logic(uint32_t N)
    : messageQueue(0)
    , readBuffer(new std::vector<Particle>(N))
    , writeBuffer(new std::vector<Particle>(N))
    , dragged(-1)
{
}

void Logic::start(int width, int height)
{
    this->width = width;
    this->height = height;
    assert(this->width > Particle::Radius);
    assert(this->height > Particle::Radius);

    std::random_device rd;
    std::mt19937 gen(rd());
    std::uniform_real_distribution<> dis_x(0 + Particle::Radius, this->width - Particle::Radius);
    std::uniform_real_distribution<> dis_y(0 + Particle::Radius, this->height - Particle::Radius);
    for (Particle& p : *readBuffer) {
        p.vx = 0;
        p.vy = 0;
        p.x = dis_x(gen);
        p.y = dis_y(gen);
    }

    workerThread = std::thread(&Logic::run, this);
}

void Logic::finish()
{
    messageQueue.push(Message(Message::Type::Stop));
    workerThread.join();
}

void Logic::updateParticles()
{
    for (auto i = 0U; i < readBuffer->size(); ++i)
    {
        double x, y, vx, vy;
        const Particle& p1 = readBuffer->at(i);

        if (i != dragged)
        {
            double ax = 0;
            double ay = 0;

            for (auto j = 0U; j < readBuffer->size(); ++j)
            {
                if (j == dragged) continue;
                if (i == j) continue;

                const Particle& p2 = readBuffer->at(j);
                auto dx = p2.x - p1.x;
                auto dy = p2.y - p1.y;
                double r = std::sqrt(dx*dx + dy*dy);
                r = std::max(r, 1e-6);

                ax += dx / (r*r) - dx / (r*r*r);
                ay += dy / (r*r) - dy / (r*r*r);
            }


            x = p1.x + dt * p1.vx;
            vx = p1.vx + dt * ax;
            if (x < Particle::Radius or x > width - Particle::Radius) vx *= -1;
            x = std::min(std::max(x, Particle::Radius), width - Particle::Radius);

            y = p1.y + dt * p1.vy;
            vy = p1.vy + dt * ay;
            if (y < Particle::Radius or y > height - Particle::Radius) vy *= -1;
            y = std::min(std::max(y, Particle::Radius), height - Particle::Radius);
        }
        else
        {
            x = p1.x;
            y = p1.y;
            vx = 0;
            vy = 0;
        }

        (*writeBuffer)[i] = {
            x, y,
            vx, vy
        };
    }
}

std::size_t Logic::findParticle(int x, int y)
{
    auto i = 0U;
    for (; i < writeBuffer->size(); ++i) {
        const Particle& p = writeBuffer->at(i);
        auto dx = p.x - x;
        auto dy = p.y - y;
        auto r = std::sqrt(dx*dx + dy*dy);
        if (r <= Particle::Radius) {
            break;
        }
    }
    return i;
}

bool Logic::processMessages()
{
    Message m;
    while (messageQueue.pop(m))
    {
        switch (m.type)
        {
            case Message::Type::Click:
            {
                auto i = findParticle(m.x, m.y);
                if (i < writeBuffer->size())
                {
                    writeBuffer->erase(writeBuffer->begin() + i);
                }
                else
                {
                    writeBuffer->push_back({double(m.x), double(m.y), 0, 0});
                }

                break;
            }

            case Message::Type::Drag:
            {
                auto i = findParticle(m.x, m.y);
                if (i < writeBuffer->size())
                {
                    dragged = i;
                }

                break;
            }
            case Message::Type::Move:
            {
                if (dragged < writeBuffer->size()) {
                    (*writeBuffer)[dragged] = {
                        double(m.x), double(m.y),
                        0, 0
                    };
                }
                break;
            }
            case Message::Type::Release:
            {
                if (dragged < writeBuffer->size()) {
                    (*writeBuffer)[dragged] = {
                        double(m.x), double(m.y),
                        0, 0
                    };
                }
                dragged = -1;
                break;
            }

            case Message::Type::Resize:
            {
                width = m.x;
                height = m.y;

                break;
            }
            case Message::Type::Stop:
            {
                return true;
            }
            default:
            {
                std::cerr << "Unexpected message: " << m.type << std::endl;
                std::exit(1);
            }

        }
    }

    return false;
}

void Logic::swapBuffers()
{
    std::lock_guard<std::mutex> guard(swap_mutex);
    std::swap(readBuffer, writeBuffer);
}

void Logic::run()
{
    while (true)
    {
        writeBuffer->resize(readBuffer->size());

        updateParticles();

        if (processMessages()) return;

        swapBuffers();

        std::this_thread::sleep_for(std::chrono::milliseconds(1));

    }
}
