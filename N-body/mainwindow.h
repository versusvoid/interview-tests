#ifndef MAINWINDOW_H
#define MAINWINDOW_H

#include <QWidget>
#include <QResizeEvent>
#include <QPen>

#include "logic.h"

class MainWindow : public QWidget
{
    Q_OBJECT

public:
    explicit MainWindow(QWidget *parent = 0);
    ~MainWindow();

protected:
    void paintEvent(QPaintEvent *event) Q_DECL_OVERRIDE;

    void timerEvent(QTimerEvent *event) Q_DECL_OVERRIDE;

    void resizeEvent(QResizeEvent *event) Q_DECL_OVERRIDE;

    void mousePressEvent(QMouseEvent *event) Q_DECL_OVERRIDE;
    void mouseMoveEvent(QMouseEvent *event) Q_DECL_OVERRIDE;
    void mouseReleaseEvent(QMouseEvent *event) Q_DECL_OVERRIDE;

    void closeEvent(QCloseEvent *event) Q_DECL_OVERRIDE;
private:
    Logic logic;
    bool dragging;

    QPen particlePen;
    QBrush particleBrush;

};

#endif // MAINWINDOW_H
